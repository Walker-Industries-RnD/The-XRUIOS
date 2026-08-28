using Spectre.Console;  // Add this using
using System.Data;
using System.Text;
using XRUIOS.Manager;
using Rule = Spectre.Console.Rule;

try { Console.Clear(); } catch { /* no console: redirected pipe or running as a service */ }

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║   ██╗  ██╗██████╗ ██╗   ██╗██╗ ██████╗ ███████╗         ║
║   ╚██╗██╔╝██╔══██╗██║   ██║██║██╔═══██╗██╔════╝         ║
║    ╚███╔╝ ██████╔╝██║   ██║██║██║   ██║███████╗         ║
║    ██╔██╗ ██╔══██╗██║   ██║██║██║   ██║╚════██║         ║
║   ██╔╝ ██╗██║  ██║╚██████╔╝██║╚██████╔╝███████║         ║
║   ╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝ ╚═════╝ ╚══════╝         ║
║                                                           ║
║                   XRUIOS Manager v1.0                     ║
║              Trusted Core · Secure Gateway                ║
╚═══════════════════════════════════════════════════════════╝
");
Console.ResetColor();

Console.OutputEncoding = Encoding.UTF8;
AnsiConsole.Write(new Rule("[green]▶[/] [grey]Secure Gateway Active[/] [green]◀[/]")
    .RuleStyle("green dim")
    .Centered());

AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]] System initializing...[/]");
AnsiConsole.WriteLine();

string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "run";
string[] rest = args.Length > 1 ? args[1..] : System.Array.Empty<string>();

return verb switch
{
    "run"               => await ManagerHost.RunAsync(rest),
    "run-service"       => await ServiceHost.RunAsync(rest),
    "start"             => ManagerHost.StartDetached(rest),
    "login"             => await ManagerHost.LoginAsync(rest),
    "status"            => ManagerHost.Status(),
    "stop"              => ManagerHost.Stop(),
    "install"           => AutostartInstaller.Install(),
    "uninstall"         => AutostartInstaller.Uninstall(),
    "install-service"   => AutostartInstaller.InstallService(),
    "uninstall-service" => AutostartInstaller.UninstallService(),
    _                   => Unknown(verb),
};

static int Unknown(string verb)
{
    System.Console.Error.WriteLine(
        $"Unknown verb '{verb}'. Try: run | start | login | status | stop | install | uninstall | install-service | uninstall-service");
    return 1;
}
