using System.Security.Cryptography;
using System.Text;
using EclipseProject;
using Grpc.Net.Client;
using MagicOnion.Client;
using static EclipseProject.Security;
using static Pariah_Cybersecurity.EasyPQC;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Client side of the Eclipse secure channel: the drop-in replacement for the old
    /// <c>MagicOnionClient.Create&lt;IPublicAcc&gt;()</c> + direct call.
    ///
    /// Wraps the full enroll, Kyber handshake and transcript check (see Eclipse's Program.cs
    /// walkthrough) so callers just do:
    /// <code>
    /// await using var session = await EclipseSecureClient.ConnectAsync(addr, "xruios-core");
    /// var acc = await session.InvokeAsync&lt;PublicAccount&gt;("GetAccInfo", new() { ["accountName"] = user });
    /// </code>
    /// The <see cref="Identity"/> is the caller's id that the server's PSK lookup and permission
    /// checks key on.
    ///
    /// Calls on one client are serialized (every message carries the next sequence number), and a
    /// session the server dropped for being idle is re-established transparently on the next call.
    /// </summary>
    public sealed class EclipseSecureClient : IAsyncDisposable
    {
        // One handshaked session. Replaced when the server reports it expired.
        private sealed record Session(GrpcChannel Channel, IDiracService Api, AeadChannel ToServer, AeadChannel FromServer);

        private readonly string _address;
        private readonly string _clientName;
        private readonly byte[] _psk;
        private readonly Dictionary<string, byte[]>? _managerSigningKey;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Session _session;

        /// <summary>The caller's Eclipse identity for this client.</summary>
        public string Identity { get; }

        private EclipseSecureClient(Session session, string address, string clientName, byte[] psk,
            string identity, Dictionary<string, byte[]>? managerSigningKey)
        {
            _session = session;
            _address = address;
            _clientName = clientName;
            _psk = psk;
            _managerSigningKey = managerSigningKey;
            Identity = identity;
        }

        /// <param name="psk">
        /// The peer's pre-shared key. For a worker, only the XRUIOS.Manager holds this; for the
        /// Manager broker, it's the per-app key the Manager issued. Without it the handshake fails.
        /// </param>
        /// <param name="identity">
        /// Stable caller identity. For app-to-Manager this MUST be the Manager-issued appId (the server
        /// resolves the PSK and checks permissions by it). Omit for a random per-session id.
        /// </param>
        /// <param name="managerSigningKey">
        /// The Manager's PRIVATE identity key, supplied only on Manager-to-worker connects. When present,
        /// we sign our clientId with it and carry the signature in place of the name, so the worker can
        /// cryptographically confirm it's talking to the real Manager (not just a PSK-holder).
        /// </param>
        public static async Task<EclipseSecureClient> ConnectAsync(
            string serverAddress, string clientName, byte[] psk, string? identity = null,
            Dictionary<string, byte[]>? managerSigningKey = null)
        {
            // Allow cleartext HTTP/2 for the local loopback worker (no TLS on-machine).
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // Identity is either the caller-supplied stable id (app path) or a random per-session id.
            // The PSK is supplied by the caller, not minted here, so only the key-holder authenticates.
            string clientId = identity ?? Guid.NewGuid().ToString();
            var session = await HandshakeAsync(serverAddress, clientName, psk, clientId, managerSigningKey);
            return new EclipseSecureClient(session, serverAddress, clientName, psk, clientId, managerSigningKey);
        }

        private static async Task<Session> HandshakeAsync(string serverAddress, string clientName, byte[] psk,
            string clientId, Dictionary<string, byte[]>? managerSigningKey)
        {
            var channel = GrpcChannel.ForAddress(serverAddress);
            var api = MagicOnionClient.Create<IDiracService>(channel);

            // Manager path: prove our identity by signing the clientId; carry the signature as the name.
            if (managerSigningKey != null)
                clientName = Convert.ToBase64String(await PqcGate.RunAsync(() => Signatures.CreateSignature(managerSigningKey, clientId)));

            Dictionary<string, byte[]> pubKey = await api.EnrollAsync(clientName, clientId);

            // Handshake: Kyber encapsulation -> shared secret; exchange nonces.
            var secret = await PqcGate.RunAsync(() => Keys.CreateSecret(pubKey));
            byte[] sharedSecret = secret.key;
            byte[] cipher = secret.text;
            byte[] nonceC = RandomNumberGenerator.GetBytes(16);

            var serverResp = await api.BeginHandshakeAsync(clientId, cipher, nonceC);

            var keys = await PqcGate.RunAsync(() => PrepareKeys(psk, nonceC, serverResp.nonceS, sharedSecret));

            byte[] transcriptHash = SHA256.HashData(ByteArrayExtensions.Combine(
                Encoding.UTF8.GetBytes(clientId), cipher, nonceC, serverResp.nonceS, serverResp.sessionId,
                BitConverter.GetBytes(serverResp.epoch)));

            var clientChannel = new AeadChannel(keys.k_c2s, serverResp.sessionId, clientId, 1,
                new Transcript(transcriptHash, "client-finished"));
            var serverChannel = new AeadChannel(keys.k_s2c, serverResp.sessionId, clientId, 1,
                new Transcript(transcriptHash, "server-finished"));

            if (clientChannel.transcript.proof == null || serverChannel.transcript.proof == null)
                throw new Exception("Handshake failed: null HMAC proof.");

            byte[] serverTranscriptRaw = await api.FinishHandshakeAsync(clientId, clientChannel.transcript.proof);
            if (!CryptographicOperations.FixedTimeEquals(serverTranscriptRaw, serverChannel.transcript.proof))
                throw new Exception("Handshake failed: incorrect server transcript HMAC.");

            return new Session(channel, api, clientChannel, serverChannel);
        }

        // Run one call. If the server says the session expired (idle too long), handshake again and
        // retry once; the server rejected the call before running anything, so that's safe.
        private async Task<TResult> CallAsync<TResult>(Func<Session, Task<TResult>> call)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    return await call(_session).ConfigureAwait(false);
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
                {
                    Close(_session);
                    _session = await HandshakeAsync(_address, _clientName, _psk, Identity, _managerSigningKey).ConfigureAwait(false);
                    return await call(_session).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Invoke a worker capability over the encrypted channel and decrypt the reply.</summary>
        public Task<T> InvokeAsync<T>(string capability, Dictionary<string, object?>? args = null)
        {
            args ??= new Dictionary<string, object?>();
            return CallAsync(async s =>
            {
                byte[] serializedEnv = s.ToServer.PackAndEncrypt(capability, args);
                byte[] serializedResp = await s.Api.InvokeAsync(serializedEnv);
                return s.FromServer.UnpackResponse<T>(serializedResp);
            });
        }

        /// <summary>
        /// Invoke a capability and return the DECRYPTED result bytes without deserializing them.
        /// The Manager broker uses this to relay a worker's exact result to an app: it decrypts the
        /// worker-to-Manager reply here, then re-encrypts the same bytes onto the Manager-to-app channel,
        /// so end-to-end confidentiality holds and the app deserializes to its own concrete type.
        /// </summary>
        public Task<byte[]> InvokeRawAsync(string capability, Dictionary<string, object?> args)
        {
            return CallAsync(async s =>
            {
                byte[] serializedEnv = s.ToServer.PackAndEncrypt(capability, args);
                byte[] serializedResp = await s.Api.InvokeAsync(serializedEnv);

                var resp = MessagePack.MessagePackSerializer.Deserialize<EclipseLCL.DiracResponse>(serializedResp);
                if (!resp.Success)
                    throw new Exception($"Worker call failed: {resp.ServerMessage}");

                var data = MessagePack.MessagePackSerializer.Deserialize<EncryptedEnvelope>(resp.EncryptedData);
                return s.FromServer.Decrypt(data); // plaintext result bytes
            });
        }

        private static void Close(Session s)
        {
            s.ToServer.Dispose();
            s.FromServer.Dispose();
            s.Channel.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync();
            try
            {
                await _session.Api.FinishAsync(_session.ToServer.PackAndEncrypt<bool>("terminate", true));
            }
            catch
            {
                // Best-effort teardown.
            }
            finally
            {
                Close(_session);
                _gate.Release();
            }
        }
    }
}
