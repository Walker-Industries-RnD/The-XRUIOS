using System.Text;
using System.Security.Cryptography;
using PermissionHandler;
using WISecureData;
using Spectre.Console;

// Aether Engine mod permission test.
//
// Aether's CasSandbox (ModRegistry / ModProfile) keys every mod's grants on XRUIOS.Permissions - the
// SAME store and gate the XRUIOS Manager uses for apps. A mod's ModId (its Notary-verified reverse-DNS
// package id) is just a requester id in that store. This test drives PermissionHandler.Handler
// directly to show the gate: a granted mod passes, an ungranted one is denied - exactly what the
// sandbox enforces before a mod can reach a capability like the user's calendar.

Console.OutputEncoding = Encoding.UTF8;
AnsiConsole.Write(new FigletText("XRUIOS").Color(Color.White).Centered());
AnsiConsole.Write(new Rule("[red]AETHER ENGINE[/] [grey]· CasSandbox mod gate · via XRUIOS.Permissions[/]").RuleStyle("red dim").Centered());
AnsiConsole.WriteLine();

// The host's shared permission store (in Aether this is the engine's own Handler + AppKey).
string saveDir = Path.Combine(Path.GetTempPath(), "xruios_aether_modtest_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(saveDir);
const string permClass = "aether-mods";
byte[] masterKey = RandomNumberGenerator.GetBytes(32);

var handler = new Handler();
if (!await handler.PermissionFileExists(SD(permClass), SD(saveDir)))
    await handler.SetupPermFile(SD(saveDir), SD(permClass), SDk(masterKey));

// Two mods, enrolled by reverse-DNS package id (Notary-verified in the real sandbox).
const string calendarMod = "com.hollvania.calendarwidget";  // a well-behaved calendar widget
const string evilMod     = "com.evil.dataslurp";            // wants everything, granted nothing

// Grant the calendar widget exactly one capability. The sketchy mod gets none.
await handler.ChangePermission(SD(saveDir), SD(permClass), SD("Time.Calendar:GetEvents"), SD(calendarMod), SDk(masterKey), SDInt(1));

AnsiConsole.MarkupLine($"[grey]host store:[/] [aqua]{Markup.Escape(saveDir)}[/]");
AnsiConsole.MarkupLine($"[green]enrolled[/] [white]{calendarMod}[/]  [grey]granted[/] [green]Time.Calendar:GetEvents[/]");
AnsiConsole.MarkupLine($"[green]enrolled[/] [white]{evilMod}[/]  [grey]granted[/] [red]nothing[/]\n");

// Probe both mods against two capabilities and render the gate's verdicts.
string[] caps = { "Time.Calendar:GetEvents", "Time.Calendar:DeleteEvent" };
var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
table.AddColumn("[white]mod[/]");
foreach (var c in caps) table.AddColumn($"[white]{Markup.Escape(c)}[/]");

foreach (var mod in new[] { calendarMod, evilMod })
{
    var cells = new List<string> { $"[aqua]{mod}[/]" };
    foreach (var c in caps)
    {
        bool ok = await handler.CheckPermission(SD(saveDir), SD(permClass), SD(c), SD(mod), SDk(masterKey), requiredLevel: 1);
        cells.Add(ok ? "[green]ALLOW[/]" : "[red]DENY[/]");
    }
    table.AddRow(cells.ToArray());
}
AnsiConsole.Write(table);

AnsiConsole.MarkupLine("\n[grey]The calendar widget reads events; it cannot delete them. The data-slurp mod is denied[/]");
AnsiConsole.MarkupLine("[grey]outright. One XRUIOS.Permissions authority gates apps and Aether mods alike.[/]");

try { Directory.Delete(saveDir, true); } catch { }

// SecureData zeroes the buffer it's handed, so every call gets a FRESH array.
static SecureData SD(string s) => new SecureData(Encoding.UTF8.GetBytes(s));
static SecureData SDk(byte[] b) => new SecureData((byte[])b.Clone());
static SecureData SDInt(int v) => new SecureData(BitConverter.GetBytes(v));
