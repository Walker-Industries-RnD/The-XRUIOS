using System.Threading;

namespace XRUIOS.Manager
{
    // Only one Manager per device. The guard holds a global named mutex for the life of the process;
    // a second Manager sees it already taken and steps aside. Workers are separate and unaffected.
    public sealed class SingletonGuard : IDisposable
    {
        private readonly Mutex _mutex;

        public bool IsPrimary { get; }

        public SingletonGuard(string name = @"Global\XRUIOS.Manager.Singleton")
        {
            _mutex = new Mutex(initiallyOwned: true, name, out bool created);
            IsPrimary = created;
        }

        public void Dispose()
        {
            if (IsPrimary)
            {
                try { _mutex.ReleaseMutex(); } catch { /* not held */ }
            }
            _mutex.Dispose();
        }
    }
}
