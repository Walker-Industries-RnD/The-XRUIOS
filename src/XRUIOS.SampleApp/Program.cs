using System.Text;
using XRUIOS.Interfaces;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;


// XRUIOS wordmark stays white; the Höllvania chrome is green.
AnsiConsole.Write(new FigletText("XRUIOS").Color(Color.White).Centered());
AnsiConsole.Write(new Rule("[green]HÖLLVANIA MUNICIPAL CALENDAR[/] [grey]· WALKERSOFT99 - Feeding the Signal, One Byte At A Time. · est. 1999[/]").RuleStyle("green dim").Centered());
AnsiConsole.MarkupLine("[grey]   powered by the XRUIOS.Manager · Plagues Protocol secured link[/]\n");

// ── credentials: env handoff -> dev creds file -> self-enroll ──
string? appId = Environment.GetEnvironmentVariable("XRUIOS_APP_ID");
string? pskB64 = Environment.GetEnvironmentVariable("XRUIOS_APP_PSK");
string? brokerAddr = Environment.GetEnvironmentVariable("XRUIOS_BROKER_ADDR");
if (appId is null || pskB64 is null || brokerAddr is null)
    (appId, pskB64, brokerAddr) = TryDevCredsFile();
if (appId is null || pskB64 is null || brokerAddr is null)
{
    try
    {
        var prov = await XruiosEnrollment.EnrollAsync("sampleapp");
        appId = prov.AppId; pskB64 = prov.PskBase64; brokerAddr = prov.BrokerAddress;
    }
    catch (Exception ex)
    {
        AnsiConsole.Write(new Panel($"[red]ENROLLMENT REFUSED[/]\n[grey]{Markup.Escape(ex.Message)}[/]\n\nIs the Manager running + unlocked?  [white]XRUIOS.Manager login[/] then [white]start[/].")
            .Header("[red] modem [/]").BorderColor(Color.Red));
        return;
    }
}
byte[] appPsk = Convert.FromBase64String(pskB64!);
AnsiConsole.MarkupLine($"   [grey]> MODEM: ATDT MANAGER ......... [/][green]CARRIER ESTABLISHED[/]");
AnsiConsole.MarkupLine($"   [grey]IDENT[/] [aqua]{Markup.Escape(appId!)}[/]   [grey]LINE[/] [aqua]{Markup.Escape(brokerAddr!)}[/]\n");

try
{
    EclipseSecureClient mgr = null!;
    await AnsiConsole.Status().Spinner(Spinner.Known.BouncingBar).SpinnerStyle("green")
        .StartAsync("Negotiating post-quantum handshake (KYBER / AES-256-GCM)...", async _ =>
        {
            mgr = await EclipseSecureClient.ConnectAsync(brokerAddr!, "sample-app", appPsk, identity: appId!);
        });
    AnsiConsole.MarkupLine("   [green]✓ SECURE.[/] [grey]Nobody on this machine can read this line.[/]\n");

    string day = DateTime.Today.ToString("yyyy-MM-dd");

    // granted: write an event
    AnsiConsole.MarkupLine($"   [aqua][[QUERY]][/] [white]AddEvent({day}, \"Ship XRUIOS\")[/] [grey]-> Time.Calendar worker[/]");
    string uid = await mgr.InvokeAsync<string>("AddEvent",
        new Dictionary<string, object?> { ["day"] = day, ["summary"] = "Ship XRUIOS" });
    AnsiConsole.MarkupLine($"   [green]✓ event created[/] [grey]{Markup.Escape(uid)}[/]\n");

    // granted: read the day
    AnsiConsole.MarkupLine($"   [aqua][[QUERY]][/] [white]GetEvents({day})[/] [grey]-> routed by the operator[/]");
    string events = await mgr.InvokeAsync<string>("GetEvents",
        new Dictionary<string, object?> { ["day"] = day });
    var rows = events.Split(';').Select(e => $"[white]▸[/] {Markup.Escape(e.Trim())}");
    AnsiConsole.Write(new Panel(string.Join("\n", rows))
        .Header("[yellow] TODAY IN HÖLLVANIA [/]").BorderColor(Color.Yellow).Padding(1, 0));

    // denied: a capability this app was never granted
    AnsiConsole.MarkupLine($"\n   [aqua][[QUERY]][/] [white]DeleteEvent(...)[/] [grey]-> privileged, NOT granted[/]");
    try
    {
        await mgr.InvokeAsync<string>("DeleteEvent", new Dictionary<string, object?> { ["uid"] = uid });
        AnsiConsole.MarkupLine("   [red]✖ THE WIPE WENT THROUGH - permissions are broken![/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.Write(new Panel($"[red]╳ ACCESS DENIED BY THE OPERATOR ╳[/]\n[grey]{Markup.Escape(ex.Message)}[/]\n[grey](granted GetEvents + AddEvent, never DeleteEvent)[/]")
            .BorderColor(Color.Red));
    }

    await mgr.DisposeAsync();
    AnsiConsole.MarkupLine("\n   [green]> LOGOFF. Stay groovy, Höllvania. //  NO CARRIER[/]");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"\n   [red]✖ LINE DROPPED:[/] [grey]{Markup.Escape(ex.Message)}[/]");
}

static (string?, string?, string?) TryDevCredsFile()
{
    try
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XRUIOS", "Public", "sampleapp.creds");
        if (!File.Exists(path)) return (null, null, null);
        var kv = File.ReadAllLines(path).Select(l => l.Split('=', 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        return (kv.GetValueOrDefault("AppId"), kv.GetValueOrDefault("Psk"), kv.GetValueOrDefault("Broker"));
    }
    catch { return (null, null, null); }
}
