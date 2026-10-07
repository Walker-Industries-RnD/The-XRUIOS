using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Apps.Shared;

/// <summary>
/// Runs YOUR assistant for the whole XRUIOS. Give it an <see cref="IAssistantInterpreter"/> (a local
/// model, a remote API, a rules engine) and it answers every question asked through PocketPal: it
/// takes AssistantAsked events, asks your interpreter, and posts the reply with AnswerAssistant.
/// Questions asked while no provider was running are picked up at start.
/// <code>
/// await using var pal = await AssistantProvider.StartAsync(broker, new MyModel());
/// </code>
/// The provider app needs: AnswerAssistant, PendingQuestions and Events.AssistantAsked.
/// </summary>
public sealed class AssistantProvider : IAsyncDisposable
{
    private readonly WorkerBroker _broker;
    private readonly IAssistantInterpreter _brain;
    private readonly Action<Exception>? _onError;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private XruiosEventStream? _stream;

    private AssistantProvider(WorkerBroker broker, IAssistantInterpreter brain, Action<Exception>? onError)
    {
        _broker = broker;
        _brain = brain;
        _onError = onError;
    }

    public static async Task<AssistantProvider> StartAsync(WorkerBroker broker, IAssistantInterpreter brain, Action<Exception>? onError = null)
    {
        var p = new AssistantProvider(broker, brain, onError);
        p._stream = broker.Subscribe(e =>
        {
            if (e.Name != XruiosEventNames.AssistantAsked) return;
            var q = EventJson.Read<AssistantQuestion>(e.Payload);
            _ = p.AnswerAsync(q.Id, q.Prompt);
        }, onError);

        // Anything asked before we were running.
        var pending = await broker.InvokeAsync<string>("PendingQuestions");
        foreach (var row in pending.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = row.Split('|');
            if (f.Length >= 2) _ = p.AnswerAsync(f[0], f[1]);
        }
        return p;
    }

    private async Task AnswerAsync(string id, string prompt)
    {
        try { await _oneAtATime.WaitAsync(_cts.Token); }
        catch (OperationCanceledException) { return; }
        try
        {
            string answer = await _brain.AskAsync(prompt, _cts.Token);
            await _broker.InvokeAsync<string>("AnswerAssistant", new Dictionary<string, object?> { ["id"] = id, ["answer"] = answer });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _onError?.Invoke(ex); }
        finally { _oneAtATime.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_stream != null) await _stream.DisposeAsync();
        _cts.Dispose();
    }
}
