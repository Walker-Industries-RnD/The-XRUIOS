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
    /// Client side of the Eclipse secure channel — the drop-in replacement for the old
    /// <c>MagicOnionClient.Create&lt;IPublicAcc&gt;()</c> + direct call.
    ///
    /// Wraps the full enroll → Kyber handshake → transcript check dance (see Eclipse's
    /// Program.cs walkthrough) so callers just do:
    /// <code>
    /// await using var session = await EclipseSecureClient.ConnectAsync(addr, "xruios-core");
    /// var acc = await session.InvokeAsync&lt;PublicAccount&gt;("GetAccInfo", new() { ["accountName"] = user });
    /// </code>
    /// The random <see cref="Identity"/> minted here is the caller's UUID that the worker's
    /// permission gate keys on.
    /// </summary>
    public sealed class EclipseSecureClient : IAsyncDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly IDiracService _api;
        private readonly AeadChannel _clientChannel;
        private readonly AeadChannel _serverChannel;

        /// <summary>The caller's Eclipse identity (UUID) for this session.</summary>
        public string Identity { get; }

        private EclipseSecureClient(GrpcChannel channel, IDiracService api, AeadChannel clientChannel,
            AeadChannel serverChannel, string identity)
        {
            _channel = channel;
            _api = api;
            _clientChannel = clientChannel;
            _serverChannel = serverChannel;
            Identity = identity;
        }

        /// <param name="psk">
        /// The peer's pre-shared key. For a worker, only the XRUIOS.Manager holds this; for the
        /// Manager broker, it's the per-app key the Manager issued. Without it the handshake fails.
        /// </param>
        /// <param name="identity">
        /// Stable caller identity. For app→Manager this MUST be the Manager-issued appId (the server
        /// resolves the PSK and checks permissions by it). Omit for a random per-session id.
        /// </param>
        /// <param name="managerSigningKey">
        /// The Manager's PRIVATE identity key, supplied only on Manager→worker connects. When present,
        /// we sign our clientId with it and carry the signature in place of the name, so the worker can
        /// cryptographically confirm it's talking to the real Manager (not just a PSK-holder).
        /// </param>
        public static async Task<EclipseSecureClient> ConnectAsync(
            string serverAddress, string clientName, byte[] psk, string? identity = null,
            Dictionary<string, byte[]>? managerSigningKey = null)
        {
            // Allow cleartext HTTP/2 for the local loopback worker (no TLS on-machine).
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var channel = GrpcChannel.ForAddress(serverAddress);
            var api = MagicOnionClient.Create<IDiracService>(channel);

            // Identity is either the caller-supplied stable id (app path) or a random per-session id.
            // The PSK is supplied by the caller — not minted here — so only the key-holder authenticates.
            string clientId = identity ?? Guid.NewGuid().ToString();

            // Manager path: prove our identity by signing the clientId; carry the signature as the name.
            if (managerSigningKey != null)
                clientName = Convert.ToBase64String(await Signatures.CreateSignature(managerSigningKey, clientId));

            Dictionary<string, byte[]> pubKey = await api.EnrollAsync(clientName, clientId);

            // Handshake: Kyber encapsulation -> shared secret; exchange nonces.
            var secret = Keys.CreateSecret(pubKey);
            byte[] sharedSecret = secret.key;
            byte[] cipher = secret.text;
            byte[] nonceC = RandomNumberGenerator.GetBytes(16);

            var serverResp = await api.BeginHandshakeAsync(clientId, cipher, nonceC);

            var keys = PrepareKeys(psk, nonceC, serverResp.nonceS, sharedSecret);

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

            return new EclipseSecureClient(channel, api, clientChannel, serverChannel, clientId);
        }

        /// <summary>Invoke a worker capability over the encrypted channel and decrypt the reply.</summary>
        public async Task<T> InvokeAsync<T>(string capability, Dictionary<string, object?>? args = null)
        {
            args ??= new Dictionary<string, object?>();
            byte[] serializedEnv = _clientChannel.PackAndEncrypt(capability, args);
            byte[] serializedResp = await _api.InvokeAsync(serializedEnv);
            return _serverChannel.UnpackResponse<T>(serializedResp);
        }

        /// <summary>
        /// Invoke a capability and return the DECRYPTED result bytes without deserializing them.
        /// The Manager broker uses this to relay a worker's exact result to an app: it decrypts the
        /// worker→Manager reply here, then re-encrypts the same bytes onto the Manager→app channel,
        /// so end-to-end confidentiality holds and the app deserializes to its own concrete type.
        /// </summary>
        public async Task<byte[]> InvokeRawAsync(string capability, Dictionary<string, object?> args)
        {
            byte[] serializedEnv = _clientChannel.PackAndEncrypt(capability, args);
            byte[] serializedResp = await _api.InvokeAsync(serializedEnv);

            var resp = MessagePack.MessagePackSerializer.Deserialize<EclipseLCL.DiracResponse>(serializedResp);
            if (!resp.Success)
                throw new Exception($"Worker call failed: {resp.ServerMessage}");

            var data = MessagePack.MessagePackSerializer.Deserialize<EncryptedEnvelope>(resp.EncryptedData);
            return _serverChannel.Decrypt(data); // plaintext result bytes
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _api.FinishAsync(_clientChannel.PackAndEncrypt<bool>("terminate", true));
            }
            catch
            {
                // Best-effort teardown.
            }
            finally
            {
                _clientChannel.Dispose();
                _serverChannel.Dispose();
                _channel.Dispose();
            }
        }
    }
}
