namespace XRUIOS.Interfaces
{
    /// <summary>
    /// App / host side of the event channel: keeps a dedicated session to the Manager and long-polls
    /// Events.Poll, calling <c>onEvent</c> for everything this app has been granted. Reconnects on its
    /// own (Manager restart, idle-expired session) and resumes from its cursor, so events aren't lost
    /// across a blip.
    /// <code>
    /// await using var stream = XruiosEventStream.Start(broker, appId, psk, e =>
    ///     InterpreterEvents.Dispatch(e.Name, e.Payload));
    /// </code>
    /// </summary>
    public sealed class XruiosEventStream : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        /// <summary>Last cursor seen; -1 until the first reply (= "only events from now on").</summary>
        public long Cursor { get; private set; } = -1;

        private XruiosEventStream(string broker, string appId, byte[] psk,
            Func<XruiosEvent, Task> onEvent, Action<Exception>? onError, Action? onReset)
        {
            _loop = Task.Run(() => RunAsync(broker, appId, psk, onEvent, onError, onReset, _cts.Token));
        }

        public static XruiosEventStream Start(string brokerAddress, string appId, byte[] psk,
            Action<XruiosEvent> onEvent, Action<Exception>? onError = null, Action? onReset = null)
            => new(brokerAddress, appId, psk, e => { onEvent(e); return Task.CompletedTask; }, onError, onReset);

        public static XruiosEventStream Start(string brokerAddress, string appId, byte[] psk,
            Func<XruiosEvent, Task> onEvent, Action<Exception>? onError = null, Action? onReset = null)
            => new(brokerAddress, appId, psk, onEvent, onError, onReset);

        private async Task RunAsync(string broker, string appId, byte[] psk, Func<XruiosEvent, Task> onEvent,
            Action<Exception>? onError, Action? onReset, CancellationToken ct)
        {
            var backoff = TimeSpan.FromSeconds(1);
            while (!ct.IsCancellationRequested)
            {
                EclipseSecureClient? client = null;
                try
                {
                    client = await EclipseSecureClient.ConnectAsync(broker, "xruios-events", psk,
                        identity: appId + XruiosEventProtocol.StreamIdentitySuffix);
                    backoff = TimeSpan.FromSeconds(1);

                    while (!ct.IsCancellationRequested)
                    {
                        string json = await client.InvokeAsync<string>(XruiosEventProtocol.PollCapability,
                            new() { ["after"] = Cursor, ["waitMs"] = (long)20_000 });
                        var batch = XruiosEventBatch.FromJson(json);
                        if (batch.Reset) onReset?.Invoke();
                        foreach (var e in batch.Events)
                        {
                            try { await onEvent(e); }
                            catch (Exception ex) { onError?.Invoke(ex); }   // a bad handler must not kill the stream
                        }
                        Cursor = batch.Cursor;
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    onError?.Invoke(ex);
                    try { await Task.Delay(backoff, ct); } catch (OperationCanceledException) { break; }
                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 15));
                }
                catch (OperationCanceledException) { break; }
                finally
                {
                    if (client != null) { try { await client.DisposeAsync(); } catch { /* going away */ } }
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            // An in-flight poll can't be interrupted (the call takes no token); don't wait it out.
            await Task.WhenAny(_loop, Task.Delay(2000));
            if (_loop.IsCompleted) _cts.Dispose();
        }
    }
}
