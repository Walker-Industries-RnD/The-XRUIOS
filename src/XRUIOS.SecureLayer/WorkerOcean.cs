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

        public async Task<DiracResponse> HandleRequestAsync(
            DiracRequest request,
            AeadChannel toClient,
            string requesterUuid,
            IPermissionGate gate)
        {
            // Permission first — before we even confirm the function exists — so a
            // refused caller learns nothing about which capabilities are present.
            if (!gate.Check(requesterUuid, request.FunctionName))
                return new DiracResponse(false, Array.Empty<byte>(),
                    $"Permission denied for '{request.FunctionName}'.");

            if (!_ark.TryGetValue(request.FunctionName, out var regFunc))
                return new DiracResponse(false, Array.Empty<byte>(), "Capability not found in Plagues worker.");

            try
            {
                var attr = regFunc.Attribute;

                if (attr.ParameterTypes.Length != request.Parameters.Count)
                    throw new Exception("Parameter count mismatch.");

                // Parameters arrive as an ordered map (name -> value). We invoke by
                // position; the MessagePack PrimitiveObjectFormatter already decoded
                // primitives (string/int/…) on the way in.
                object?[] args = new object?[attr.ParameterTypes.Length];
                int i = 0;
                foreach (var kv in request.Parameters)
                    args[i++] = kv.Value;

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

                // Serialize as the concrete declared type (not typeless object) so the
                // client can deserialize it back with the matching type.
                Type payloadType = UnwrapTaskType(regFunc.Method.ReturnType);
                byte[] serializedResult = result == null
                    ? MessagePackSerializer.Serialize<object?>(null)
                    : MessagePackSerializer.Serialize(payloadType, result);

                var env = toClient.Encrypt(request.FunctionName, serializedResult);
                byte[] serializedEnv = MessagePackSerializer.Serialize(env);

                return new DiracResponse(true, serializedEnv, "Success");
            }
            catch (TargetInvocationException tie)
            {
                return new DiracResponse(false, Array.Empty<byte>(),
                    tie.InnerException?.Message ?? tie.Message);
            }
            catch (Exception ex)
            {
                return new DiracResponse(false, Array.Empty<byte>(), ex.Message);
            }
        }

        private static Type UnwrapTaskType(Type returnType)
        {
            if (returnType == typeof(Task)) return typeof(object);
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                return returnType.GetGenericArguments()[0];
            return returnType;
        }
    }
}
