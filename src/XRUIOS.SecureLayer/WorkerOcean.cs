using System.Reflection;
using EclipseLCL;
using EclipseProject;
using MessagePack;
using static EclipseProject.Security;

namespace XRUIOS.Interfaces
{
    // The worker-side capability registry + dispatcher.
    //
    // Eclipse's own Ocean (EclipseProject.Ocean) only floods methods from Eclipse's
    // OWN assembly (Assembly.GetExecutingAssembly()), so it can never see a worker's
    // [SeaOfDirac] methods when Eclipse is consumed as a plain DLL. WorkerOcean is the
    // Plagues-side equivalent: it scans the WORKER assembly we hand it, reuses Eclipse's
    // public crypto (AeadChannel) for the response, and gates every call through the
    // Manager's IPermissionGate before it runs.
    public sealed class WorkerOcean
    {
        private sealed class RegisteredFunction
        {
            public required Type DeclaringType { get; init; }
            public required MethodInfo Method { get; init; }
            public required SeaOfDirac Attribute { get; init; }
        }

        private readonly Dictionary<string, RegisteredFunction> _ark = new(StringComparer.Ordinal);

        public WorkerOcean(Assembly capabilityAssembly)
        {
            foreach (var type in capabilityAssembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    var attr = method.GetCustomAttribute<SeaOfDirac>();
                    if (attr != null)
                    {
                        _ark[attr.Name] = new RegisteredFunction
                        {
                            DeclaringType = type,
                            Method = method,
                            Attribute = attr
                        };
                    }
                }
            }
        }

        public IReadOnlyCollection<string> Capabilities => _ark.Keys;

        /// <summary>Where every served call is recorded (null = not audited, e.g. a dev standalone run).</summary>
        public AuditTrail? Audit { get; init; }

        /// <summary>
        /// Argument keys starting with this prefix are call metadata the Manager attaches (e.g. which app
        /// asked), never capability parameters. They're stripped before binding.
        /// </summary>
        public const string MetaPrefix = "__xruios.";
        public const string CallerKey = MetaPrefix + "caller";

        /// <summary>Reserved (never announced): the Manager asks for this worker's audit head to anchor it.</summary>
        public const string AuditHeadCapability = "__xruios.audithead";

        public async Task<DiracResponse> HandleRequestAsync(
            DiracRequest request,
            AeadChannel toClient,
            string requesterUuid,
            IPermissionGate gate)
        {
            // Permission first — before we even confirm the function exists — so a
            // refused caller learns nothing about which capabilities are present.
            if (!gate.Check(requesterUuid, request.FunctionName))
            {
                Audit?.Append(requesterUuid, "deny", request.FunctionName, ok: false, "session not opened by the Manager");
                return new DiracResponse(false, Array.Empty<byte>(),
                    $"Permission denied for '{request.FunctionName}'.");
            }

            // The Manager's event pump (reserved name, never announced, so apps can't be routed here).
            if (request.FunctionName == XruiosEventProtocol.WorkerPollCapability)
            {
                string? epoch = request.Parameters.TryGetValue("epoch", out var ep) ? ep as string : null;
                var batch = await WorkerEvents.PollAsync(epoch, Long(request.Parameters, "after"), (int)Long(request.Parameters, "waitMs"));
                return Reply(request.FunctionName, batch.ToJson(), typeof(string), toClient);
            }

            if (request.FunctionName == AuditHeadCapability)
            {
                var (seq, mac) = Audit?.Head() ?? (0, "none");
                return Reply(request.FunctionName, $"{seq}|{mac}", typeof(string), toClient);
            }

            if (!_ark.TryGetValue(request.FunctionName, out var regFunc))
                return new DiracResponse(false, Array.Empty<byte>(), "Capability not found in Plagues worker.");

            string? stamped = request.Parameters.TryGetValue(CallerKey, out var c) ? c as string : null;
            string caller = stamped ?? requesterUuid;
            WorkerCall.CallerSlot.Value = stamped;
            var started = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                object?[] args = Bind(regFunc.Method, request.Parameters);

                object? instance = regFunc.Method.IsStatic
                    ? null
                    : Activator.CreateInstance(regFunc.DeclaringType);

                object? result = regFunc.Method.Invoke(instance, args);

                // Support capabilities declared as async (Task / Task<T>).
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    result = regFunc.Method.ReturnType.IsGenericType
                        ? regFunc.Method.ReturnType.GetProperty("Result")!.GetValue(task)
                        : null;
                }

                Audit?.Append(caller, "call", request.FunctionName, ok: true, $"{started.ElapsedMilliseconds} ms");

                // Serialize as the concrete declared type (not typeless object) so the
                // client can deserialize it back with the matching type.
                return Reply(request.FunctionName, result, UnwrapTaskType(regFunc.Method.ReturnType), toClient);
            }
            catch (Exception ex)
            {
                string message = ex is TargetInvocationException tie ? tie.InnerException?.Message ?? tie.Message : ex.Message;
                Audit?.Append(caller, "call", request.FunctionName, ok: false, message);
                return new DiracResponse(false, Array.Empty<byte>(), message);
            }
        }

        /// <summary>
        /// Turn the request's argument map into the method's parameter list. When every supplied key
        /// names a parameter, binding is by name, and parameters with a C# default may be left out (so a
        /// capability can grow an optional argument without breaking older callers). Otherwise the old
        /// positional rule applies: exactly one value per parameter, in order.
        /// </summary>
        private static object?[] Bind(MethodInfo method, IReadOnlyDictionary<string, object?> raw)
        {
            var ps = method.GetParameters();
            var supplied = raw.Where(kv => !kv.Key.StartsWith(MetaPrefix, StringComparison.Ordinal)).ToList();
            var args = new object?[ps.Length];

            var byName = ps.ToDictionary(p => p.Name!, StringComparer.Ordinal);
            if (supplied.All(kv => byName.ContainsKey(kv.Key)))
            {
                var given = supplied.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                for (int i = 0; i < ps.Length; i++)
                {
                    if (given.TryGetValue(ps[i].Name!, out var v)) args[i] = v;
                    else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                    else throw new Exception($"Missing parameter '{ps[i].Name}'.");
                }
                return args;
            }

            if (supplied.Count != ps.Length)
                throw new Exception("Parameter count mismatch.");
            for (int i = 0; i < ps.Length; i++) args[i] = supplied[i].Value;
            return args;
        }

        private static DiracResponse Reply(string functionName, object? result, Type payloadType, AeadChannel toClient)
        {
            byte[] serializedResult = result == null
                ? MessagePackSerializer.Serialize<object?>(null)
                : MessagePackSerializer.Serialize(payloadType, result);

            var env = toClient.Encrypt(functionName, serializedResult);
            return new DiracResponse(true, MessagePackSerializer.Serialize(env), "Success");
        }

        private static long Long(IReadOnlyDictionary<string, object?> args, string key) =>
            args.TryGetValue(key, out var v) && v != null ? Convert.ToInt64(v) : 0;

        private static Type UnwrapTaskType(Type returnType)
        {
            if (returnType == typeof(Task)) return typeof(object);
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                return returnType.GetGenericArguments()[0];
            return returnType;
        }
    }
}
