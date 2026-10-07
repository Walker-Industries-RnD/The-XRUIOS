using XRUIOS.Interpreter;
using XRUIOS.Interpreter.Probe;

// XRUIOS interpreter probe: run one interpreter command on this machine and print the result.
//   probe list                              every command + its arguments
//   probe audio.list                        read-only
//   probe audio.master.set volume=40        key=value arguments
//   probe selftest                          check the Linux parsers + wire format (any OS)
if (args.Length > 0 && args[0] == "selftest") return SelfTest.Run();

if (args.Length == 0 || args[0] is "list" or "help" or "-h" or "--help")
{
    Console.WriteLine($"XRUIOS interpreter probe - backend: {XruiosInterpreter.Current.Platform}\n");
    foreach (var (name, help) in InterpreterCommands.List())
        Console.WriteLine($"  {name,-22} {help}");
    Console.WriteLine($"  {"selftest",-22} check the Linux parsers + wire format (any OS)");
    return 0;
}

var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var a in args.Skip(1))
{
    int eq = a.IndexOf('=');
    if (eq <= 0) { Console.Error.WriteLine($"Arguments are key=value, got '{a}'."); return 2; }
    kv[a[..eq]] = a[(eq + 1)..];
}

try
{
    Console.WriteLine(InterpreterCommands.Execute(args[0], kv));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    return 1;
}
