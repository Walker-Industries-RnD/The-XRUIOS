using WISecureData;

namespace XRUIOS.Contracts
{
    /// <summary>
    /// The authoritative runtime context a worker needs: where its data lives, and the key its store is
    /// encrypted under. The XRUIOS.Manager is the authority — it generates the canonical paths and a
    /// per-worker key, then hands each worker its own <see cref="IXruiosContext"/> at launch. Copied
    /// system code still reads the static <c>XRUIOS</c> shim, which delegates here once bound.
    /// </summary>
    public interface IXruiosContext
    {
        /// <summary>Private data root for this worker's encrypted stores.</summary>
        string DataPath { get; }

        /// <summary>Public data root (discovery copies readable before login).</summary>
        string PublicDataPath { get; }

        /// <summary>The per-worker encryption key. Held by the Manager; never shared between workers.</summary>
        SecureData EncryptionKey { get; }
    }
}
