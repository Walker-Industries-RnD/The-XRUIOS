namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Pariah's post-quantum primitives (Kyber encapsulation, Dilithium sign/verify) are not safe to
    /// call from several threads at once: concurrent handshakes in one process failed with "Failed To
    /// Create Secret" on the client and "incorrect transcript HMAC" on the server (the two sides
    /// derived different keys). Every handshake's crypto step goes through this one gate. Only the
    /// crypto is held - never a network round trip - and a handshake's crypto is a few milliseconds,
    /// so queueing costs nothing noticeable.
    /// </summary>
    internal static class PqcGate
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task<T> RunAsync<T>(Func<Task<T>> crypto)
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try { return await crypto().ConfigureAwait(false); }
            finally { Gate.Release(); }
        }

        public static async Task<T> RunAsync<T>(Func<T> crypto)
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try { return crypto(); }
            finally { Gate.Release(); }
        }
    }
}
