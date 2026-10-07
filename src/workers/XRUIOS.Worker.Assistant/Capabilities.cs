using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using static Pariah_Cybersecurity.DataHandler;

namespace XRUIOS.Worker.Assistant
{
    // PocketPal, the local assistant. The XRUIOS doesn't pick the model: like weather, the answering is
    // done by a PROVIDER app the developer writes (an IAssistantInterpreter behind AssistantProvider in
    // XRUIOS.Apps.Shared). The flow:
    //
    //   app      -> Ask(prompt)              this worker stores the question, publishes AssistantAsked
    //   provider <- AssistantAsked           (it holds Events.AssistantAsked), runs its model
    //   provider -> AnswerAssistant(id, ...) stored, published as AssistantAnswered
    //   app      <- Ask returns the answer   if it came within waitMs; otherwise "pending|<id>" and the
    //                                         answer arrives later as AssistantAnswered
    //
    // With no provider installed, questions stay pending and the history shows them unanswered.
    public static class AssistantCapabilities
    {
        private const int MaxWaitMs = 20000;
        private const int HistoryLimit = 200;

        private sealed record Pending(AssistantQuestion Question, TaskCompletionSource<string> Answer);
        private sealed record Turn(string Id, string Prompt, string? Answer, string AskedBy, DateTimeOffset At);

        private static readonly ConcurrentDictionary<string, Pending> Waiting = new(StringComparer.Ordinal);
        private static readonly SemaphoreSlim StoreGate = new(1, 1);

        private static string Dir => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Assistant");

        [SeaOfDirac("Assistant.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Assistant: " + input;

        /// <summary>Ask PocketPal. Returns the answer, or "pending|&lt;id&gt;" if none came within waitMs.</summary>
        [SeaOfDirac("Ask", new[] { "prompt", "waitMs" }, typeof(string), typeof(string), typeof(long))]
        public static async Task<string> Ask(string prompt, long waitMs = 15000)
        {
            if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Ask needs a prompt.");
            Expire();

            var q = new AssistantQuestion(Guid.NewGuid().ToString("N"), prompt.Trim(),
                WorkerCall.CallerAppId ?? "unknown", DateTimeOffset.Now);
            var pending = new Pending(q, new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
            Waiting[q.Id] = pending;
            await SaveTurnAsync(new Turn(q.Id, q.Prompt, null, q.AskedBy, q.At));
            WorkerEvents.Publish(XruiosEventNames.AssistantAsked, EventJson.Write(q));

            var wait = Task.Delay((int)Math.Clamp(waitMs, 0, MaxWaitMs));
            return await Task.WhenAny(pending.Answer.Task, wait) == pending.Answer.Task
                ? pending.Answer.Task.Result
                : "pending|" + q.Id;
        }

        /// <summary>The provider's reply to an AssistantAsked question.</summary>
        [SeaOfDirac("AnswerAssistant", new[] { "id", "answer" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AnswerAssistant(string id, string answer)
        {
            var history = await LoadAsync();
            var turn = history.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"No question with id '{id}'.");

            await SaveTurnAsync(turn with { Answer = answer });
            WorkerEvents.Publish(XruiosEventNames.AssistantAnswered,
                EventJson.Write(new AssistantAnswer(id, turn.Prompt, answer, DateTimeOffset.Now)));
            if (Waiting.TryRemove(id, out var p)) p.Answer.TrySetResult(answer);
            return "ok";
        }

        /// <summary>Questions nobody has answered yet: "id|prompt;..." (for a provider that just started).</summary>
        [SeaOfDirac("PendingQuestions", new string[0], typeof(string))]
        public static async Task<string> PendingQuestions()
        {
            var open = (await LoadAsync()).Where(t => t.Answer is null).OrderBy(t => t.At);
            return string.Join(";", open.Select(t => $"{t.Id}|{Wire.Field(t.Prompt)}"));
        }

        /// <summary>Newest first: "id|prompt|answer|askedAt;..." (answer is empty while pending).</summary>
        [SeaOfDirac("AssistantHistory", new[] { "count" }, typeof(string), typeof(long))]
        public static async Task<string> AssistantHistory(long count = 20)
        {
            var turns = (await LoadAsync()).OrderByDescending(t => t.At).Take((int)Math.Clamp(count, 1, HistoryLimit));
            return string.Join(";", turns.Select(t =>
                $"{t.Id}|{Wire.Field(t.Prompt)}|{Wire.Field(t.Answer ?? "")}|{t.At:yyyy-MM-ddTHH:mm:ss}"));
        }

        // A caller that gave up long ago doesn't need its waiter kept alive.
        private static void Expire()
        {
            foreach (var (id, p) in Waiting)
                if (p.Question.At < DateTimeOffset.Now.AddMinutes(-10)) Waiting.TryRemove(id, out _);
        }

        // ---- store: one encrypted JSON list of turns, newest HistoryLimit kept ----

        private static async Task<List<Turn>> LoadAsync()
        {
            if (!File.Exists(Path.Combine(Dir, "History.json"))) return new();
            var store = await JSONDataHandler.LoadJsonFile("History", Dir);
            if (!await JSONDataHandler.CheckIfVariableExists(store, "Data")) return new();
            var json = (string)await JSONDataHandler.GetVariable<string>(store, "Data", global::XRUIOS.Barebones.XRUIOS.encryptionKey);
            return string.IsNullOrEmpty(json) ? new() : EventJson.Read<List<Turn>>(json);
        }

        private static async Task SaveTurnAsync(Turn turn)
        {
            await StoreGate.WaitAsync();
            try
            {
                var turns = await LoadAsync();
                turns.RemoveAll(t => t.Id == turn.Id);
                turns.Add(turn);
                if (turns.Count > HistoryLimit) turns = turns.OrderByDescending(t => t.At).Take(HistoryLimit).ToList();
                string json = EventJson.Write(turns);

                Directory.CreateDirectory(Dir);
                if (!File.Exists(Path.Combine(Dir, "History.json")))
                    await JSONDataHandler.CreateJsonFile("History", Dir, new JsonObject());
                var store = await JSONDataHandler.LoadJsonFile("History", Dir);
                store = await JSONDataHandler.CheckIfVariableExists(store, "Data")
                    ? await JSONDataHandler.UpdateJson<string>(store, "Data", json, global::XRUIOS.Barebones.XRUIOS.encryptionKey)
                    : await JSONDataHandler.AddToJson<string>(store, "Data", json, global::XRUIOS.Barebones.XRUIOS.encryptionKey);
                await JSONDataHandler.SaveJson(store);
            }
            finally { StoreGate.Release(); }
        }
    }
}
