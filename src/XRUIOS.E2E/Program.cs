using System.Collections.Concurrent;
using XRUIOS.Interfaces;

// End-to-end tests through the real Manager broker.
//
// The built-in apps (XRUIOS.App.*) connect with the plaintext .creds files the Manager writes under
// `run --dev-creds`. The harness itself is an INSTALLED third-party app ("e2e.harness"): it gets its
// credentials by attested enrollment over the control pipe, which is also part of what is tested.
//
// Run it with run-e2e.ps1 (it starts a Manager on a throwaway data root, so no real data is touched).
// Options:  --only name[,name]   run just those sections     --list   print the section names
//
// Calls that change something undo it where the capability allows. PowerRequest is never called.

int pass = 0, fail = 0;
string root = Environment.GetEnvironmentVariable("XRUIOS_ROOT")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XRUIOS");
string credsDir = Path.Combine(root, "Public");
var cleanupEvents = new List<string>();

AppProvisioning? prov = null;     // set by the enrollment section
byte[] mePsk = Array.Empty<byte>();
EclipseSecureClient? me = null;   // the harness's own session (an installed app)

var sections = new List<(string Name, Func<Task> Run)>
{
    ("regression", Regression),
    ("burst", Burst),
    ("clock", Clock),
    ("calendars", Calendars),
    ("albums", Albums),
    ("enrollment", Enrollment),
    ("assistant", Assistant),
    ("events", Events),
    ("alarm", Alarm),
    ("timer", Timer),
    ("recent", Recent),
    ("processes", Processes),
    ("notes", Notes),
    ("areas", Areas),
    ("datamanager", DataManager),
    ("creators", Creators),
    ("mediatags", MediaTags),
    ("identity", Identity),
    ("songs", Songs),
    ("musicplayer", MusicPlayer),
};

var only = ParseOnly(args);
if (args.Contains("--list")) { Console.WriteLine(string.Join(Environment.NewLine, sections.Select(s => s.Name))); return 0; }

foreach (var (name, run) in sections)
{
    // Enrollment always runs: the later sections use the harness's own session.
    if (only != null && !only.Contains(name) && name != "enrollment") continue;
    Console.WriteLine($"[{name}]");
    try { await run(); }
    catch (Exception ex) { Check(false, $"section '{name}' threw", ex.Message); }
}

if (me != null)
    foreach (var uid in cleanupEvents)
        await Call(me, "DeleteEvent", new() { ["uid"] = uid }, r => r.StartsWith("Deleted"), label: "cleanup DeleteEvent");

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;

// ---------------------------------------------------------------------------------------------
// Sections
// ---------------------------------------------------------------------------------------------

async Task Regression()
{
    var vol = await As("XRUIOS.App.VolumeManager");
    var ch = await Call(vol, "ListChannels", ok: r => r.StartsWith("master|Master|"));
    await Call(vol, "BatteryStatus", expectDenied: true, label: "VolumeManager may not read BatteryStatus");
    var pwr = await As("XRUIOS.App.Power");
    await Call(pwr, "BatteryStatus", ok: r => r.Split('|').Length == 4);
}

// The permission store allows only 100 checks a minute per app. Without the Manager remembering verdicts,
// the 101st call inside a minute failed with an opaque error, so any app calling faster than about one
// call per 0.6 seconds was cut off.
async Task Burst()
{
    var time = await As("XRUIOS.App.Time");
    int ok = 0;
    string? firstError = null;
    for (int i = 0; i < 300; i++)
    {
        try { await time.InvokeAsync<string>("Now", new() { ["tz"] = "" }); ok++; }
        catch (Exception ex) { firstError ??= $"call {i + 1}: {ex.Message}"; }
    }
    Check(ok == 300, "300 calls in a row all succeed (the store itself allows 100 checks a minute)", firstError ?? $"{ok}/300");
}

async Task Clock()
{
    var time = await As("XRUIOS.App.Time");
    await Call(time, "Now", new() { ["tz"] = "" }, r => r.Split('|').Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(r.Split('|')[0], @"^\d{1,2}[:.]\d{2}"));
    await Call(time, "Now", new() { ["tz"] = "UTC" }, r => r.Split('|')[4] == "UTC", label: "Now in another zone");

    var sw = await Call(time, "StopwatchStart", ok: r => r.Length == 32);
    await Task.Delay(1100);
    await Call(time, "StopwatchLap", new() { ["id"] = sw }, r => r.StartsWith("1|1"));
    await Call(time, "StopwatchElapsed", new() { ["id"] = sw }, r => r.StartsWith("00:00:01"));
    await Call(time, "StopwatchStop", new() { ["id"] = sw }, r => r.Split(';').Length == 2);
    await CallExpectingError(time, "StopwatchElapsed", new() { ["id"] = sw }, "does not exist", "a stopped stopwatch is gone");
}

async Task Calendars()
{
    var cal = await As("XRUIOS.App.Calendar");
    string today = DateTime.Today.AddHours(15).ToString("yyyy-MM-ddTHH:mm:ss");
    await Call(cal, "ListCalendars", ok: r => r.StartsWith("default|Calendar|#"));
    var calId = await Call(cal, "CreateCalendar", new() { ["name"] = "E2E Test", ["color"] = "" }, r => r == "e2e-test");
    var u1 = await Call(cal, "AddEvent", new() { ["calendarId"] = calId, ["day"] = today, ["summary"] = "e2e in its calendar" }, r => Guid.TryParse(r, out _), label: "AddEvent with calendarId");
    var u2 = await Call(cal, "AddEvent", new() { ["day"] = today, ["summary"] = "e2e default" }, r => Guid.TryParse(r, out _), label: "AddEvent, old 2-arg form");
    if (u1 != null) cleanupEvents.Add(u1);
    if (u2 != null) cleanupEvents.Add(u2);
    await Call(cal, "GetEvents", new() { ["day"] = today, ["calendarId"] = "e2e-test" }, r => r.Contains("e2e in its calendar") && !r.Contains("e2e default"), label: "GetEvents filtered to one calendar");
    await Call(cal, "ListCalendars", ok: r => r.Contains("e2e-test|E2E Test|"));
}

async Task Albums()
{
    var gal = await As("XRUIOS.App.Gallery");
    await Call(gal, "ListAlbums");
    var album = await Call(gal, "CreateAlbum", new() { ["name"] = "e2e album", ["description"] = "" }, r => r.Length > 10);
    await Call(gal, "ListAlbums", ok: r => r.Contains($"{album}|e2e album|0"), label: "ListAlbums shows the new album");
}

async Task Enrollment()
{
    try { prov = await XruiosEnrollment.EnrollAsync("e2e.harness"); Check(prov.AppId == "app:e2e.harness", "enrolled over the pipe as app:e2e.harness"); }
    catch (Exception ex) { Check(false, "enroll e2e.harness", ex.Message); }

    try { await XruiosEnrollment.EnrollAsync("e2e.other"); Check(false, "enrolling as another app's exe should be refused"); }
    catch (Exception ex) { Check(ex.Message.Contains("path mismatch"), "enrolling as another app is refused", ex.Message); }

    try { await XruiosEnrollment.EnrollAsync("no.such.app"); Check(false, "enrolling an unknown app should be refused"); }
    catch (Exception ex) { Check(ex.Message.Contains("no such registered app"), "enrolling an unknown app is refused", ex.Message); }

    if (prov == null) return;
    mePsk = Convert.FromBase64String(prov.PskBase64);
    me = await EclipseSecureClient.ConnectAsync(prov.BrokerAddress, "e2e.harness", mePsk, identity: prov.AppId);
}

async Task Assistant()
{
    if (me == null || prov == null) { Check(false, "assistant section needs the harness session"); return; }

    var pal = await As("XRUIOS.App.PocketPal");
    await Call(pal, "Recent", expectDenied: true, label: "PocketPal's legacy 'Recent' grant was revoked");

    // A question asked while no provider is running stays pending.
    var pending = await Call(pal, "Ask", new() { ["prompt"] = "first question", ["waitMs"] = 300L }, r => r.StartsWith("pending|"));
    string pendingId = pending!.Split('|')[1];
    await Call(me, "PendingQuestions", ok: r => r.Contains(pendingId), label: "provider sees the question asked before it started");

    // Now run a provider that echoes.
    var answered = new ConcurrentQueue<XruiosEvent>();
    var (brokerAddr, appId, psk) = (prov.BrokerAddress, prov.AppId, mePsk);
    await using var palStream = Stream("XRUIOS.App.PocketPal", e => answered.Enqueue(e));
    await using var provider = XruiosEventStream.Start(brokerAddr, appId, psk, e =>
    {
        if (e.Name != "AssistantAsked") return;
        var q = XRUIOS.Interpreter.EventJson.Read<XRUIOS.Interpreter.AssistantQuestion>(e.Payload);
        _ = Task.Run(async () =>
        {
            await using var c = await EclipseSecureClient.ConnectAsync(brokerAddr, "e2e.harness", psk, identity: appId);
            await c.InvokeAsync<string>("AnswerAssistant", new() { ["id"] = q.Id, ["answer"] = "echo: " + q.Prompt });
        });
    });
    await WaitFor(() => provider.Cursor >= 0 && palStream.Cursor >= 0, TimeSpan.FromSeconds(30));

    await Call(me, "AnswerAssistant", new() { ["id"] = pendingId, ["answer"] = "late answer" }, r => r == "ok", label: "provider answers the old question");
    await Call(pal, "Ask", new() { ["prompt"] = "2+2?" }, r => r == "echo: 2+2?", label: "Ask returns the provider's answer");
    await WaitFor(() => answered.Count >= 2, TimeSpan.FromSeconds(15));
    Check(answered.Any(e => e.Payload.Contains("late answer")), "PocketPal got the late answer as AssistantAnswered", $"{answered.Count} event(s)");
    await Call(pal, "AssistantHistory", new() { ["count"] = 5L }, r => r.Contains("echo: 2+2?") && r.Contains("late answer"));
}

async Task Events()
{
    var vol = await As("XRUIOS.App.VolumeManager");
    var ch = await Call(vol, "ListChannels", ok: r => r.StartsWith("master|Master|"));
    int master = int.Parse(ch!.Split(';')[0].Split('|')[2]);

    var gotVolume = new ConcurrentQueue<XruiosEvent>();
    var gotPower = new ConcurrentQueue<XruiosEvent>();
    await using var volStream = Stream("XRUIOS.App.VolumeManager", e => gotVolume.Enqueue(e));
    await using var pwrStream = Stream("XRUIOS.App.Power", e => gotPower.Enqueue(e));
    await WaitFor(() => volStream.Cursor >= 0 && pwrStream.Cursor >= 0, TimeSpan.FromSeconds(30));

    await Call(vol, "SetVolume", new() { ["channelId"] = "master", ["volume"] = master }, r => r == "ok");
    await WaitFor(() => gotVolume.Any(e => e.Name == "VolumeChanged"), TimeSpan.FromSeconds(30));
    Check(gotVolume.Any(e => e.Name == "VolumeChanged" && e.Source == "XRUIOS.Worker.Volume"), "VolumeManager received VolumeChanged");
    await Task.Delay(1500);
    Check(!gotPower.Any(e => e.Name == "VolumeChanged"), "Power app did NOT receive it (no grant)");
}

async Task Alarm()
{
    var time = await As("XRUIOS.App.Time");
    var gotTime = new ConcurrentQueue<XruiosEvent>();
    await using var timeStream = Stream("XRUIOS.App.Time", e => gotTime.Enqueue(e));
    await WaitFor(() => timeStream.Cursor >= 0, TimeSpan.FromSeconds(30));

    string label = "xruios-e2e-" + Guid.NewGuid().ToString("N")[..6];
    await Call(time, "AddAlarm", new() { ["when"] = DateTime.Now.AddSeconds(20).ToString("O"), ["label"] = label }, r => r.StartsWith(label));
    Console.WriteLine("  ... waiting for the alarm (20 s out, the scheduler polls about every 15 s)");
    await WaitFor(() => gotTime.Any(e => e.Name == "AlarmFired" && e.Payload.Contains(label)), TimeSpan.FromSeconds(75));
    Check(gotTime.Any(e => e.Name == "AlarmFired" && e.Payload.Contains(label)), "Time app received AlarmFired");
    await Call(time, "DeleteAlarm", new() { ["label"] = label }, r => r.StartsWith("deleted"));
}


// ---- the workers that used to answer only Ping ----

async Task Timer()
{
    if (me == null || prov == null) { Check(false, "timer needs the harness session"); return; }
    var finished = new ConcurrentQueue<XruiosEvent>();
    await using var events = MyStream(e => { if (e.Name == "TimerFinished") finished.Enqueue(e); });
    await WaitFor(() => events.Cursor >= 0, TimeSpan.FromSeconds(30));

    await Call(me, "StartTimer", new() { ["name"] = "e2e-short", ["seconds"] = 2L }, r => DateTime.TryParse(r, out _));
    await Call(me, "ListTimers", ok: r => r.StartsWith("e2e-short|"));
    await WaitFor(() => !finished.IsEmpty, TimeSpan.FromSeconds(20));
    Check(finished.Any(e => e.Payload.Contains("e2e-short")), "TimerFinished arrived", finished.TryPeek(out var f) ? f.Payload : "no event");
    await Call(me, "ListTimers", ok: r => !r.Contains("e2e-short"), label: "a finished timer is gone from the list");

    await Call(me, "StartTimer", new() { ["name"] = "e2e-long", ["seconds"] = 60L });
    await Call(me, "AddTimerTime", new() { ["name"] = "e2e-long", ["seconds"] = 30L }, r => DateTime.TryParse(r, out _));
    await Call(me, "ListTimers", ok: r => r.Contains("e2e-long|") && int.Parse(r.Split('|')[2]) > 60, label: "AddTimerTime extended it");
    await Call(me, "CancelTimer", new() { ["name"] = "e2e-long" }, r => r == "cancelled");
    await CallExpectingError(me, "CancelTimer", new() { ["name"] = "e2e-long" }, "No running timer", "cancelling twice is an error");
    await CallExpectingError(me, "StartTimer", new() { ["name"] = "x", ["seconds"] = 0L }, "seconds must be", "a zero-second timer is refused");
}

async Task Recent()
{
    if (me == null) { Check(false, "recent needs the harness session"); return; }
    await Call(me, "ClearRecentRecordings", ok: r => r == "cleared");
    var id1 = await Call(me, "AddRecentRecording", new() { ["path"] = @"C:\memos\one.wav" }, r => r.Length > 10);
    await Call(me, "AddRecentRecording", new() { ["path"] = @"C:\memos\two.wav" });
    await Call(me, "RecentRecordings", new() { ["limit"] = 10L }, r => r.Split(';').Length == 2 && r.Split(';')[0].EndsWith("two.wav"), label: "newest first");
    await Call(me, "RemoveRecentRecording", new() { ["id"] = id1 }, r => r == "removed");
    await Call(me, "RecentRecordings", ok: r => !r.Contains("one.wav") && r.Contains("two.wav"));
    await CallExpectingError(me, "RemoveRecentRecording", new() { ["id"] = "nope" }, "No recording", "an unknown id is an error");
    await CallExpectingError(me, "AddRecentRecording", new() { ["path"] = " " }, "needs a file path", "an empty path is refused");
    await Call(me, "ClearRecentRecordings", ok: r => r == "cleared");
    await Call(me, "RecentRecordings", ok: r => r == "");
}

async Task Processes()
{
    if (me == null) { Check(false, "processes needs the harness session"); return; }
    await Call(me, "ListProcesses", new() { ["includeCpu"] = false, ["limit"] = 5L }, r => r.Split(';').Length >= 1 && int.TryParse(r.Split(';')[0].Split('|')[0], out _));
    await Call(me, "SaveProcessSnapshot", new() { ["name"] = "e2e-snap" }, r => r == "e2e-snap");
    await Call(me, "ListProcessSnapshots", ok: r => r.Split(';').Contains("e2e-snap"));
    await Call(me, "GetProcessSnapshot", new() { ["name"] = "e2e-snap" }, r => r.Split(';').Length > 5);
    await CallExpectingError(me, "SaveProcessSnapshot", new() { ["name"] = @"..\evil" }, "snapshot name", "a snapshot name cannot climb out of its folder");
    await CallExpectingError(me, "GetProcessSnapshot", new() { ["name"] = "../x" }, "snapshot name", "reading one cannot either");
    await CallExpectingError(me, "KillProcess", new() { ["processId"] = 4L, ["processName"] = "System" }, "not a process id", "system process ids are refused");
    await CallExpectingError(me, "KillProcess", new() { ["processId"] = (long)Environment.ProcessId, ["processName"] = "notepad" }, "not 'notepad'", "a name that does not match the id is refused");
    string self = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "XRUIOS.E2E");
    await CallExpectingError(me, "KillProcess", new() { ["processId"] = (long)Environment.ProcessId, ["processName"] = self }, "own processes", "XRUIOS's own processes are never stopped");
}

async Task Notes()
{
    if (me == null) { Check(false, "notes needs the harness session"); return; }
    var id = await Call(me, "CreateJournal", new() { ["name"] = "E2E Journal", ["author"] = "tester", ["description"] = "test" }, r => r.StartsWith("E2E Journal v1 by tester__ID "));
    await Call(me, "ListJournals", ok: r => r.Contains(id!) && r.Contains("|E2E Journal|tester|1|test|0"));
    await Call(me, "ListJournalCategories", new() { ["journalId"] = id }, r => r == "");
    await Call(me, "ListFavoriteJournals", ok: r => r == "");
    await Call(me, "FavoriteJournal", new() { ["journalId"] = id }, r => r == "favorited");
    await Call(me, "ListFavoriteJournals", ok: r => r.Contains(id!));
    await Call(me, "ListJournals", ok: r => r.Contains(id!), label: "the favorites list is not mistaken for a journal");
    await Call(me, "UnfavoriteJournal", new() { ["journalId"] = id }, r => r == "removed");
    await CallExpectingError(me, "CreateJournal", new() { ["name"] = @"..\bad", ["author"] = "x" }, "may use letters", "a journal name cannot hold path characters");
    await CallExpectingError(me, "DeleteJournal", new() { ["journalId"] = @"..\..\Public\x" }, "not a journal id", "a journal id cannot climb out of its folder");
    await Call(me, "DeleteJournal", new() { ["journalId"] = id }, r => r == "deleted");
    await Call(me, "ListJournals", ok: r => !r.Contains(id!));
}

async Task Areas()
{
    if (me == null) { Check(false, "areas needs the harness session"); return; }
    var id = await Call(me, "AddWorldPoint", new() { ["name"] = "Desk", ["description"] = "my desk", ["userCentric"] = true }, r => Guid.TryParse(r, out _));
    await Call(me, "ListWorldPoints", ok: r => r.Contains($"{id}|Desk|my desk|1|0|0|0|0"));
    await Call(me, "UpdateWorldPoint", new() { ["id"] = id, ["name"] = "Standing desk", ["description"] = "", ["userCentric"] = "false" }, r => r == "updated");
    await Call(me, "ListWorldPoints", ok: r => r.Contains($"{id}|Standing desk|my desk|0|"), label: "name and flag changed, description kept");
    await CallExpectingError(me, "UpdateWorldPoint", new() { ["id"] = id, ["name"] = "", ["description"] = "", ["userCentric"] = "maybe" }, "'true' or 'false'", "a bad flag is refused");
    await CallExpectingError(me, "DeleteWorldPoint", new() { ["id"] = @"..\x" }, "not a world point id", "only a GUID is accepted as an id");
    await Call(me, "DeleteWorldPoint", new() { ["id"] = id }, r => r == "deleted");
    await Call(me, "ListWorldPoints", ok: r => !r.Contains(id!));
}

async Task DataManager()
{
    if (me == null) { Check(false, "datamanager needs the harness session"); return; }
    string wp = Guid.NewGuid().ToString();
    var sid = await Call(me, "AddSession", new() { ["title"] = "Work", ["description"] = "at the desk", ["worldPointIds"] = wp }, r => Guid.TryParse(r, out _));
    await Call(me, "ListSessions", ok: r => r.Contains($"{sid}|Work|at the desk|") && r.EndsWith("|1"));
    await Call(me, "UpdateSession", new() { ["id"] = sid, ["title"] = "Work from home", ["description"] = "" }, r => r == "updated");
    await Call(me, "ListSessions", ok: r => r.Contains($"{sid}|Work from home|at the desk|"));
    await Call(me, "SetSessionWorldPoints", new() { ["id"] = sid, ["worldPointIds"] = "" }, r => r == "updated");
    await Call(me, "ListSessions", ok: r => r.Contains($"{sid}|") && r.EndsWith("|0"), label: "an empty list clears the world points");

    var slot = await Call(me, "AddDataSlot", new() { ["title"] = "Home", ["description"] = "", ["sessionIds"] = sid }, r => Guid.TryParse(r, out _));
    await Call(me, "SetDataSlotFavorite", new() { ["id"] = slot, ["favorite"] = true }, r => r == "favorited");
    await Call(me, "ListDataSlots", ok: r => r.Contains($"{slot}|Home||1|") && r.EndsWith("|1"));
    await Call(me, "UpdateDataSlot", new() { ["id"] = slot, ["title"] = "Home base", ["description"] = "d" }, r => r == "updated");
    await Call(me, "SetDataSlotSessions", new() { ["id"] = slot, ["sessionIds"] = "" }, r => r == "updated");
    await Call(me, "ListDataSlots", ok: r => r.Contains($"{slot}|Home base|d|1|") && r.EndsWith("|0"));

    await CallExpectingError(me, "DeleteSession", new() { ["id"] = @"..\..\Data" }, "not an id", "an id cannot climb out of the folder");
    await CallExpectingError(me, "AddSession", new() { ["title"] = "x", ["description"] = "", ["worldPointIds"] = "not-a-guid" }, "not an id", "a bad world point id is refused");
    await Call(me, "DeleteDataSlot", new() { ["id"] = slot }, r => r == "deleted");
    await Call(me, "DeleteSession", new() { ["id"] = sid }, r => r == "deleted");
    await Call(me, "ListSessions", ok: r => !r.Contains(sid!));
}

async Task Creators()
{
    if (me == null) { Check(false, "creators needs the harness session"); return; }
    string dir = Path.Combine(Path.GetTempPath(), "xruios-e2e-files"); Directory.CreateDirectory(dir);
    string file = Path.Combine(dir, "cover.png"); File.WriteAllBytes(file, new byte[] { 1, 2, 3 });

    await Call(me, "ListCreators", new() { ["creatorType"] = "Music" }, r => !r.Contains("Artist One"));
    await Call(me, "CreateCreator", new() { ["name"] = "Artist One", ["creatorType"] = "Music", ["description"] = "first" }, r => r == "created");
    await Call(me, "ListCreators", new() { ["creatorType"] = "Music" }, r => r == "Artist One|first|0");
    await CallExpectingError(me, "CreateCreator", new() { ["name"] = "Artist One", ["creatorType"] = "Music", ["description"] = "" }, "already exists", "a duplicate is refused");
    await Call(me, "AddCreatorFiles", new() { ["name"] = "Artist One", ["creatorType"] = "Music", ["paths"] = file }, r => r == "added 1");
    await Call(me, "ListCreatorFiles", new() { ["name"] = "Artist One", ["creatorType"] = "Music" }, r => r.EndsWith("|cover.png"));
    await Call(me, "SetCreatorDescription", new() { ["name"] = "Artist One", ["creatorType"] = "Music", ["description"] = "changed" }, r => r == "updated");
    await Call(me, "ListCreators", new() { ["creatorType"] = "Music" }, r => r == "Artist One|changed|1");
    await Call(me, "ListFavoriteCreators", new() { ["creatorType"] = "Music" }, r => r == "");
    await Call(me, "FavoriteCreator", new() { ["name"] = "Artist One", ["creatorType"] = "Music" }, r => r == "favorited");
    await Call(me, "ListFavoriteCreators", new() { ["creatorType"] = "Music" }, r => r == "Artist One");
    await Call(me, "UnfavoriteCreator", new() { ["name"] = "Artist One", ["creatorType"] = "Music" }, r => r == "removed");

    await CallExpectingError(me, "ListCreators", new() { ["creatorType"] = "Media" }, "MediaTagger", "the type 'Media' belongs to the MediaTagger worker");
    await CallExpectingError(me, "CreateCreator", new() { ["name"] = @"..\x", ["creatorType"] = "Music", ["description"] = "" }, "may use letters", "a creator name cannot hold path characters");
    await CallExpectingError(me, "CreateCreator", new() { ["name"] = "X", ["creatorType"] = @"..\..", ["description"] = "" }, "creator type", "neither can a type");
    await CallExpectingError(me, "AddCreatorFiles", new() { ["name"] = "Artist One", ["creatorType"] = "Music", ["paths"] = @"C:\no\such\file.png" }, "Not an existing file", "a missing file is refused");
    await CallExpectingError(me, "AddCreatorFiles", new() { ["name"] = "Artist One", ["creatorType"] = "Music", ["paths"] = "relative.png" }, "full path", "a relative path is refused");
}

async Task MediaTags()
{
    if (me == null) { Check(false, "mediatags needs the harness session"); return; }
    string dir = Path.Combine(Path.GetTempPath(), "xruios-e2e-files"); Directory.CreateDirectory(dir);
    string file = Path.Combine(dir, "photo.jpg"); File.WriteAllBytes(file, new byte[] { 9, 9 });

    await Call(me, "ListMediaTags", ok: r => !r.Contains("Holiday"));
    await Call(me, "CreateMediaTag", new() { ["name"] = "Holiday", ["description"] = "summer" }, r => r == "created");
    await Call(me, "ListMediaTags", ok: r => r == "Holiday|summer|0");
    await Call(me, "AddMediaFiles", new() { ["name"] = "Holiday", ["paths"] = file }, r => r == "added 1");
    await Call(me, "ListMediaFiles", new() { ["name"] = "Holiday" }, r => r.EndsWith("|photo.jpg"));
    await Call(me, "FavoriteMediaTag", new() { ["name"] = "Holiday" }, r => r == "favorited");
    await Call(me, "ListFavoriteMediaTags", ok: r => r == "Holiday");
    await Call(me, "UnfavoriteMediaTag", new() { ["name"] = "Holiday" }, r => r == "removed");
    await CallExpectingError(me, "CreateMediaTag", new() { ["name"] = "Holiday", ["description"] = "" }, "already exists", "a duplicate tag is refused");
    await CallExpectingError(me, "AddMediaFiles", new() { ["name"] = "Holiday", ["paths"] = "nope.jpg" }, "full path", "a relative path is refused");
}

async Task Identity()
{
    if (me == null) { Check(false, "identity needs the harness session"); return; }
    var id = await Call(me, "CreateProfile", new() { ["osUser"] = "e2e-user", ["firstName"] = "Eve", ["nickname"] = "E2E" }, r => r.Length >= 10 && r.All(char.IsDigit));
    await Call(me, "ListUsers", ok: r => r.Contains($"{id}|e2e-user|Eve|E2E|"), label: "ListUsers (the Manager's own login files are not listed)");
    await Call(me, "MainUser", ok: r => r == id);
    await CallExpectingError(me, "CreateProfile", new() { ["osUser"] = @"..\x", ["firstName"] = "A", ["nickname"] = "" }, "not an account name", "an account name cannot hold path characters");
    await CallExpectingError(me, "EraseProfile", new() { ["id"] = @"..\..\Data" }, "not a profile id", "a profile id cannot climb out of its folder");
    await Call(me, "EraseProfile", new() { ["id"] = id }, r => r == "erased");
    await CallExpectingError(me, "EraseProfile", new() { ["id"] = id }, "No profile", "erasing twice is an error");
    await Call(me, "ListUsers", ok: r => !r.Contains(id!));
}

async Task Songs()
{
    if (me == null) { Check(false, "songs needs the harness session"); return; }
    string dir = Path.Combine(Path.GetTempPath(), "xruios-e2e-music"); Directory.CreateDirectory(dir);
    string wav = Path.Combine(dir, "e2e song.wav"); WriteSilentWav(wav);

    var dirId = await Call(me, "AddSongDirectory", new() { ["path"] = dir, ["name"] = "E2E Music" }, r => r.Length > 4);
    await Call(me, "ListSongDirectories", ok: r => r.Contains($"{dirId}|E2E Music|") && r.EndsWith("|1"));
    await Call(me, "ListSongs", new() { ["directoryId"] = dirId, ["onlyFavorites"] = false }, r => r.StartsWith($"{dirId}|e2e song.wav|") && r.EndsWith("e2e song.wav"));
    await Call(me, "SearchSongs", new() { ["text"] = "E2E", ["onlyFavorites"] = false }, r => r.Contains("e2e song.wav"), label: "SearchSongs ignores case");
    await Call(me, "ListFavoriteSongs", ok: r => r == "");
    await Call(me, "FavoriteSong", new() { ["directoryId"] = dirId, ["file"] = "e2e song.wav" }, r => r == "favorited");
    await Call(me, "ListFavoriteSongs", ok: r => r.Contains("e2e song.wav"));
    await Call(me, "RecordPlay", new() { ["directoryId"] = dirId, ["file"] = "e2e song.wav" }, r => r == "recorded");
    await Call(me, "RecordPlay", new() { ["directoryId"] = dirId, ["file"] = "e2e song.wav" }, r => r == "recorded", label: "playing a song twice is not an error");
    await Call(me, "ListPlayHistory", ok: r => r.Split(';').Length == 1 && r.Contains("e2e song.wav"), label: "the history holds it once");

    var pl = await Call(me, "CreatePlaylist", new() { ["name"] = "E2E list", ["description"] = "" }, r => Guid.TryParse(r, out _));
    await Call(me, "AddSongToPlaylist", new() { ["playlistId"] = pl, ["directoryId"] = dirId, ["file"] = "e2e song.wav" }, r => r == "added");
    await Call(me, "ListPlaylistSongs", new() { ["playlistId"] = pl }, r => r == $"{dirId}|e2e song.wav|1");
    await Call(me, "ListPlaylists", ok: r => r.Contains($"{pl}|E2E list||1|0"));
    await Call(me, "RemoveSongFromPlaylist", new() { ["playlistId"] = pl, ["directoryId"] = dirId, ["file"] = "e2e song.wav" }, r => r == "removed");
    await Call(me, "DeletePlaylist", new() { ["playlistId"] = pl }, r => r == "deleted");

    await CallExpectingError(me, "FavoriteSong", new() { ["directoryId"] = dirId, ["file"] = @"..\..\secret.mp3" }, "plain file name", "a song cannot be a path");
    await CallExpectingError(me, "FavoriteSong", new() { ["directoryId"] = dirId, ["file"] = "notes.txt" }, "plain file name", "only audio files are songs");
    await CallExpectingError(me, "AddSongDirectory", new() { ["path"] = "relative", ["name"] = "x" }, "full path", "a relative folder is refused");
    await CallExpectingError(me, "ListSongs", new() { ["directoryId"] = "../x", ["onlyFavorites"] = false }, "not a folder id", "a folder id cannot be a path");
    await Call(me, "UnfavoriteSong", new() { ["directoryId"] = dirId, ["file"] = "e2e song.wav" }, r => r == "removed");
    await Call(me, "ClearPlayHistory", ok: r => r == "cleared");
    await Call(me, "RemoveSongDirectory", new() { ["directoryId"] = dirId }, r => r == "removed");
    Check(File.Exists(wav), "removing a folder never deletes its files");
}

async Task MusicPlayer()
{
    if (me == null || prov == null) { Check(false, "musicplayer needs the harness session"); return; }
    var changes = new ConcurrentQueue<XruiosEvent>();
    await using var events = MyStream(e => { if (e.Name == "NowPlayingChanged") changes.Enqueue(e); });
    await WaitFor(() => events.Cursor >= 0, TimeSpan.FromSeconds(30));

    await Call(me, "NowPlaying", ok: r => r == "");
    await Call(me, "SetNowPlaying", new() { ["songId"] = "d1/a.mp3", ["title"] = "First", ["artist"] = "Band", ["album"] = "LP", ["durationSeconds"] = 215L }, r => r == "ok");
    await Call(me, "NowPlaying", ok: r => r == "d1/a.mp3|First|Band|LP|215");
    await WaitFor(() => !changes.IsEmpty, TimeSpan.FromSeconds(20));
    Check(changes.Any(e => e.Payload.Contains("First")), "NowPlayingChanged arrived", changes.TryPeek(out var c) ? c.Payload : "no event");

    await Call(me, "QueueSong", new() { ["songId"] = "q1", ["title"] = "Second", ["artist"] = "Band", ["album"] = "", ["durationSeconds"] = 100L }, r => r == "0");
    await Call(me, "QueueSong", new() { ["songId"] = "q2", ["title"] = "Third", ["artist"] = "Band", ["album"] = "", ["durationSeconds"] = 0L }, r => r == "1");
    await Call(me, "ListQueue", ok: r => r == "0|q1|Second|Band||100;1|q2|Third|Band||0");
    await Call(me, "MoveInQueue", new() { ["index"] = 0L, ["newIndex"] = 1L }, r => r == "moved");
    await Call(me, "ListQueue", ok: r => r.StartsWith("0|q2|Third"));
    await CallExpectingError(me, "RemoveFromQueue", new() { ["index"] = 9L }, "No song at that position", "an index past the end is refused");
    await Call(me, "RemoveFromQueue", new() { ["index"] = 0L }, r => r == "removed");
    await Call(me, "ClearQueue", ok: r => r == "cleared");
    await Call(me, "ListQueue", ok: r => r == "");
    await CallExpectingError(me, "SetNowPlaying", new() { ["songId"] = "x", ["title"] = "", ["artist"] = "", ["album"] = "", ["durationSeconds"] = 0L }, "needs a title", "a song without a title is refused");
    await Call(me, "ClearNowPlaying", ok: r => r == "ok");
    await Call(me, "NowPlaying", ok: r => r == "");
}

static void WriteSilentWav(string path)
{
    // 0.2 s of 8 kHz mono 8-bit silence: enough for a tag reader to accept it as audio.
    int n = 1600;
    using var w = new BinaryWriter(File.Create(path));
    w.Write("RIFF"u8.ToArray()); w.Write(36 + n); w.Write("WAVE"u8.ToArray());
    w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(8000); w.Write(8000); w.Write((short)1); w.Write((short)8);
    w.Write("data"u8.ToArray()); w.Write(n); w.Write(Enumerable.Repeat((byte)128, n).ToArray());
}

// ---------------------------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------------------------

XruiosEventStream MyStream(Action<XruiosEvent> onEvent) =>
    XruiosEventStream.Start(prov!.BrokerAddress, prov.AppId, mePsk, onEvent);

(string broker, string appId, byte[] psk) Creds(string app)
{
    var path = Path.Combine(credsDir, app + ".creds");
    if (!File.Exists(path))
        throw new FileNotFoundException($"No creds file for '{app}'. Start the Manager with `run --dev-creds` (run-e2e.ps1 does).", path);
    var kv = File.ReadAllLines(path).Select(l => l.Split('=', 2)).Where(p => p.Length == 2)
        .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
    return (kv["Broker"], kv["AppId"], Convert.FromBase64String(kv["Psk"]));
}

async Task<EclipseSecureClient> As(string app)
{
    var (broker, id, psk) = Creds(app);
    return await EclipseSecureClient.ConnectAsync(broker, "e2e", psk, identity: id);
}

XruiosEventStream Stream(string app, Action<XruiosEvent> onEvent)
{
    var (broker, id, psk) = Creds(app);
    return XruiosEventStream.Start(broker, id, psk, onEvent);
}

void Check(bool ok, string what, string detail = "")
{
    if (ok) pass++; else fail++;
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} {what}{(detail.Length > 0 ? " -> " + Trunc(detail) : "")}");
}

async Task<string?> Call(EclipseSecureClient c, string cap, Dictionary<string, object?>? a = null,
    Func<string, bool>? ok = null, bool expectDenied = false, string? label = null)
{
    try
    {
        var r = await c.InvokeAsync<string>(cap, a ?? new());
        Check(!expectDenied && (ok?.Invoke(r) ?? true), label ?? cap, r);
        return r;
    }
    catch (Exception ex)
    {
        Check(expectDenied && ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase), (label ?? cap) + " (call failed)", ex.Message);
        return null;
    }
}

async Task CallExpectingError(EclipseSecureClient c, string cap, Dictionary<string, object?> a, string errorContains, string label)
{
    try { var r = await c.InvokeAsync<string>(cap, a); Check(false, label, "no error, got: " + r); }
    catch (Exception ex) { Check(ex.Message.Contains(errorContains), label, ex.Message); }
}

static string Trunc(string s) => s.Length > 160 ? s[..160] + "..." : s;

static HashSet<string>? ParseOnly(string[] args)
{
    int i = Array.IndexOf(args, "--only");
    return i >= 0 && i + 1 < args.Length
        ? args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : null;
}

static async Task WaitFor(Func<bool> cond, TimeSpan max)
{
    var end = DateTime.UtcNow + max;
    while (!cond() && DateTime.UtcNow < end) await Task.Delay(200);
}
