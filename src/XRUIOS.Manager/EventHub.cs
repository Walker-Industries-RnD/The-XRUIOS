using XRUIOS.Interfaces;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The Manager's copy of everything the workers published, in one ordered, bounded log. Apps read
    /// it through Events.Poll (BrokerRouter), filtered to what each app is granted. Sequence numbers
    /// are the Manager's own, so an app's cursor survives any worker restarting underneath it.
    /// </summary>
    public sealed class EventHub
    {
        private const int Capacity = 2048;

        private readonly object _gate = new();
        private readonly List<XruiosEvent> _log = new();
        private long _seq;
        private TaskCompletionSource _changed = NewSignal();

        public void Append(XruiosEvent fromWorker)
        {
            TaskCompletionSource wake;
            lock (_gate)
            {
                _log.Add(fromWorker with { Seq = ++_seq });
                if (_log.Count > Capacity) _log.RemoveRange(0, _log.Count - Capacity);
                wake = _changed;
                _changed = NewSignal();
            }
            wake.TrySetResult();
        }

        /// <summary>
        /// Events after <paramref name="after"/> that pass <paramref name="allowed"/>, waiting up to
        /// <paramref name="waitMs"/> for the first one. after = -1 means "from now on".
        /// </summary>
        public async Task<XruiosEventBatch> PollAsync(long after, int waitMs, Func<string, Task<bool>> allowed)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Clamp(waitMs, 0, XruiosEventProtocol.MaxWaitMs));
            var verdicts = new Dictionary<string, bool>(StringComparer.Ordinal);   // one permission check per event name per poll
            bool reset = false;

            while (true)
            {
                List<XruiosEvent> candidates;
                long latest;
                Task wait;
                lock (_gate)
                {
                    latest = _seq;
                    long oldest = _log.Count > 0 ? _log[0].Seq : _seq + 1;
                    if (after < 0) after = latest;                                  // new subscriber: only what comes next
                    else if (after > latest) { after = 0; reset = true; }           // Manager restarted since the app's cursor
                    else if (after < oldest - 1) reset = true;                      // app fell behind the log
                    candidates = _log.Where(e => e.Seq > after).ToList();
                    wait = _changed.Task;
                }

                var visible = new List<XruiosEvent>();
                foreach (var e in candidates)
                {
                    if (!verdicts.TryGetValue(e.Name, out bool ok))
                        verdicts[e.Name] = ok = await allowed(e.Name);
                    if (ok) visible.Add(e);
                }

                // Nothing this app may see yet: skip past what was filtered out and keep waiting.
                if (candidates.Count > 0) after = candidates[^1].Seq;
                if (visible.Count > 0 || DateTime.UtcNow >= deadline)
                    return new XruiosEventBatch(after, reset, visible);

                var left = deadline - DateTime.UtcNow;
                if (left > TimeSpan.Zero) await Task.WhenAny(wait, Task.Delay(left));
            }
        }

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
