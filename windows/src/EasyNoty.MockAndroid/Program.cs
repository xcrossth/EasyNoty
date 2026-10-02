using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using EasyNoty.Core;

const string StateFile = "mock-android-state.json";
if (args.Length == 0)
{
    Console.WriteLine("EasyNoty Protocol v1 Mock Android\n  pair <wss://host:port> <pairing-token> <pc-uuid> <sha256:fingerprint> [android-uuid]\n  send [normal|ongoing|group|large|duplicate|malformed]\n  asset <file> [mime]\n  offline");
    return;
}
switch (args[0].ToLowerInvariant())
{
    case "pair": await PairAsync(args); break;
    case "send": await SendScenarioAsync(args.Length > 1 ? args[1] : "normal"); break;
    case "asset": await SendAssetAsync(args); break;
    case "offline": Console.WriteLine("Offline simulation: durable state retained; no network connection attempted."); break;
    default: throw new ArgumentException("Unknown command.");
}

static async Task PairAsync(string[] command)
{
    if (command.Length < 5) throw new ArgumentException("pair requires endpoint, token, PC UUID and certificate fingerprint.");
    var endpoint = command[1].TrimEnd('/');
    var androidId = command.Length > 5 ? command[5] : WireValues.Uuid();
    using var socket = NewSocket(command[4]);
    await socket.ConnectAsync(new Uri(endpoint + "/easynoty/pair"), CancellationToken.None);
    var request = new PairingRequest(command[2], androidId, "Mock Android", WireValues.Base64Url(RandomNumberGenerator.GetBytes(16)), ["zstd", "zlib", "event_chunking", "asset_chunking"]);
    await SendEnvelopeAsync(socket, Unsigned("pairing_request", androidId, request));
    Console.WriteLine("Pairing request sent. Confirm the six-digit code and explicitly accept it in EasyNoty Windows.");
    var response = await ReceiveEnvelopeAsync(socket);
    if (response.MessageType == "pairing_reject") throw new AuthenticationException(response.Payload.Deserialize<PairingReject>(ProtocolV1.Json)?.Message);
    var accepted = response.Payload.Deserialize<PairingAccept>(ProtocolV1.Json) ?? throw new InvalidDataException("Pair response was empty.");
    if (accepted.PcDeviceId != command[3]) throw new AuthenticationException("PC identity changed during pairing.");
    var state = new MockState(endpoint, androidId, accepted.PcDeviceId, accepted.KeyId, accepted.PairingSecret, command[4]);
    await File.WriteAllTextAsync(StateFile, JsonSerializer.Serialize(state, ProtocolV1.Json));
    Console.WriteLine($"Paired. Verification code: {accepted.VerificationCode}");
}

static async Task SendScenarioAsync(string scenario)
{
    var state = await LoadStateAsync();
    await using var session = await OpenSessionAsync(state);
    var large = scenario.Equals("large", StringComparison.OrdinalIgnoreCase);
    var raw = large
        ? JsonSerializer.SerializeToElement(new { text = new string('x', ProtocolV1.LargePayloadThreshold + 4096), title = "Large fixture" }, ProtocolV1.Json)
        : JsonSerializer.SerializeToElement(new { text = "Hello from Android", title = "Mock sender" }, ProtocolV1.Json);
    var ongoing = scenario.Equals("ongoing", StringComparison.OrdinalIgnoreCase);
    var payload = CreateNotification(raw, ongoing, scenario.Equals("group", StringComparison.OrdinalIgnoreCase));
    var malformed = scenario.Equals("malformed", StringComparison.OrdinalIgnoreCase);
    var messageId = WireValues.Uuid();
    await SendNotificationAsync(session, payload, messageId, malformed);
    Console.WriteLine(await ReadResponseAsync(session));
    if (scenario.Equals("duplicate", StringComparison.OrdinalIgnoreCase))
    {
        await SendNotificationAsync(session, payload, messageId, false);
        Console.WriteLine(await ReadResponseAsync(session));
    }
}

static async Task SendAssetAsync(string[] command)
{
    if (command.Length < 2) throw new ArgumentException("asset requires a file path.");
    var state = await LoadStateAsync();
    var bytes = await File.ReadAllBytesAsync(command[1]);
    if (bytes.Length is < 1 or > ProtocolV1.MaximumAssetBytes) throw new InvalidDataException("Asset must be between 1 byte and 25 MiB.");
    await using var session = await OpenSessionAsync(state);
    var hash = "sha256:" + Hashing.Sha256Hex(bytes);
    var transferId = WireValues.Uuid();
    var mime = command.Length > 2 ? command[2] : "application/octet-stream";
    if (bytes.Length < ProtocolV1.ChunkSize)
    {
        await SendAuthenticatedAsync(session, "asset_transfer_start", new AssetTransferStart(transferId, hash, mime, "none", bytes.Length, bytes.Length, 0, 0, Convert.ToBase64String(bytes)));
        Console.WriteLine(await ReadResponseAsync(session));
        return;
    }
    var count = (int)Math.Ceiling(bytes.Length / (double)ProtocolV1.ChunkSize);
    await SendAuthenticatedAsync(session, "asset_transfer_start", new AssetTransferStart(transferId, hash, mime, "none", bytes.Length, bytes.Length, ProtocolV1.ChunkSize, count, null));
    for (var index = 0; index < count; index++)
    {
        var offset = index * ProtocolV1.ChunkSize;
        var chunk = bytes.AsSpan(offset, Math.Min(ProtocolV1.ChunkSize, bytes.Length - offset)).ToArray();
        await SendAuthenticatedAsync(session, "asset_transfer_chunk", new AssetTransferChunk(transferId, index, offset, Convert.ToBase64String(chunk)));
    }
    await SendAuthenticatedAsync(session, "asset_transfer_complete", new AssetTransferComplete(transferId, hash, bytes.Length));
    Console.WriteLine(await ReadResponseAsync(session));
}

static async Task<SessionLease> OpenSessionAsync(MockState state)
{
    var socket = NewSocket(state.CertificateSha256);
    await socket.ConnectAsync(new Uri(state.BaseUrl + "/easynoty/sync"), CancellationToken.None);
    await SendEnvelopeAsync(socket, Unsigned("hello", state.AndroidDeviceId, new HelloPayload("android", "Mock Android", [1], ["zstd", "zlib", "event_chunking", "asset_chunking"])));
    var challengeEnvelope = await ReceiveEnvelopeAsync(socket);
    var challenge = challengeEnvelope.Payload.Deserialize<AuthChallenge>(ProtocolV1.Json) ?? throw new AuthenticationException("Auth challenge missing.");
    var clientNonce = WireValues.Base64Url(RandomNumberGenerator.GetBytes(16));
    var secret = WireValues.FromBase64Url(state.PairingSecret);
    var proof = SessionAuthentication.Proof(secret, challenge.ChallengeId, challenge.ServerNonce, clientNonce, state.AndroidDeviceId);
    await SendEnvelopeAsync(socket, Unsigned("auth_response", state.AndroidDeviceId, new AuthResponse(challenge.ChallengeId, state.KeyId, clientNonce, proof)));
    var successEnvelope = await ReceiveEnvelopeAsync(socket);
    var success = successEnvelope.Payload.Deserialize<AuthSuccess>(ProtocolV1.Json) ?? throw new AuthenticationException("Auth success missing.");
    return new(socket, state, success.SessionId, secret);
}

static NotificationEventPayload CreateNotification(JsonElement raw, bool ongoing, bool summary)
{
    var canonical = JcsCanonicalizer.Canonicalize(raw);
    return new(WireValues.Uuid(), WireValues.Uuid(), ongoing ? 1 : 0, "mock-key", "app.easynoty.mock", "EasyNoty Mock", WireValues.Utc(DateTimeOffset.UtcNow), WireValues.Utc(DateTimeOffset.UtcNow), 1,
        canonical.Length, "sha256:" + Hashing.Sha256Hex(canonical), raw,
        new("generic_android", 1, "Mock sender", "Hello from Android", null, null, "Hello from Android", 0.9), [], ongoing ? "progress" : "generic", ongoing, summary);
}

static async Task SendNotificationAsync(SessionLease session, NotificationEventPayload payload, string messageId, bool malformed)
{
    var element = JsonSerializer.SerializeToElement(payload, ProtocolV1.Json);
    var canonical = JcsCanonicalizer.Canonicalize(element);
    if (canonical.Length <= ProtocolV1.LargePayloadThreshold)
    {
        var envelope = Authenticated(session, "notification_event", element, messageId);
        if (malformed) envelope = envelope with { Authentication = envelope.Authentication! with { Signature = new string('0', 64) } };
        await SendEnvelopeAsync(session.Socket, envelope);
        return;
    }
    var transferId = WireValues.Uuid();
    var count = (int)Math.Ceiling(canonical.Length / (double)ProtocolV1.ChunkSize);
    var hash = "sha256:" + Hashing.Sha256Hex(canonical);
    await SendAuthenticatedAsync(session, "event_transfer_start", new EventTransferStart(transferId, payload.EventId, "application/vnd.easynoty.notification-event+json", "none", canonical.Length, canonical.Length, hash, ProtocolV1.ChunkSize, count));
    for (var index = 0; index < count; index++)
    {
        var offset = index * ProtocolV1.ChunkSize;
        var bytes = canonical.AsSpan(offset, Math.Min(ProtocolV1.ChunkSize, canonical.Length - offset)).ToArray();
        await SendAuthenticatedAsync(session, "event_transfer_chunk", new EventTransferChunk(transferId, index, offset, Convert.ToBase64String(bytes)));
    }
    await SendAuthenticatedAsync(session, "event_transfer_complete", new EventTransferComplete(transferId, payload.EventId, canonical.Length, hash));
}

static ProtocolEnvelope Unsigned<T>(string type, string deviceId, T payload) => new(1, WireValues.Uuid(), type, deviceId, WireValues.Utc(DateTimeOffset.UtcNow), null, JsonSerializer.SerializeToElement(payload, ProtocolV1.Json), null);
static ProtocolEnvelope Authenticated(SessionLease session, string type, JsonElement payload, string? messageId = null)
{
    var envelope = new ProtocolEnvelope(1, messageId ?? WireValues.Uuid(), type, session.State.AndroidDeviceId, WireValues.Utc(DateTimeOffset.UtcNow), session.SessionId, payload, null);
    return envelope with { Authentication = HmacAuthentication.Sign(envelope, session.State.KeyId, session.Secret) };
}
static Task SendAuthenticatedAsync<T>(SessionLease session, string type, T payload) => SendEnvelopeAsync(session.Socket, Authenticated(session, type, JsonSerializer.SerializeToElement(payload, ProtocolV1.Json)));
static Task SendEnvelopeAsync(ClientWebSocket socket, ProtocolEnvelope envelope) => socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, ProtocolV1.Json)), WebSocketMessageType.Text, true, CancellationToken.None);
static async Task<ProtocolEnvelope> ReceiveEnvelopeAsync(ClientWebSocket socket)
{
    var buffer = new byte[ProtocolV1.LargePayloadThreshold];
    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
    return JsonSerializer.Deserialize<ProtocolEnvelope>(buffer.AsSpan(0, result.Count), ProtocolV1.Json) ?? throw new InvalidDataException("Server envelope missing.");
}
static async Task<string> ReadResponseAsync(SessionLease session)
{
    var response = await ReceiveEnvelopeAsync(session.Socket);
    if (response.Authentication is null || !HmacAuthentication.Verify(response, session.Secret)) throw new AuthenticationException("Server response signature failed.");
    return JsonSerializer.Serialize(response, ProtocolV1.Json);
}
static ClientWebSocket NewSocket(string expectedFingerprint)
{
    var socket = new ClientWebSocket();
    socket.Options.RemoteCertificateValidationCallback = (_, certificate, _, errors) => ValidateCertificate(certificate, errors, expectedFingerprint);
    return socket;
}
static bool ValidateCertificate(X509Certificate? certificate, SslPolicyErrors errors, string expected)
{
    if (certificate is null) return false;
    var actual = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(certificate.GetRawCertData()));
    return actual.Equals(expected, StringComparison.Ordinal) && errors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors or SslPolicyErrors.RemoteCertificateNameMismatch;
}
static async Task<MockState> LoadStateAsync() => JsonSerializer.Deserialize<MockState>(await File.ReadAllTextAsync(StateFile), ProtocolV1.Json) ?? throw new InvalidDataException("Mock state is invalid.");

sealed record MockState(string BaseUrl, string AndroidDeviceId, string PcDeviceId, string KeyId, string PairingSecret, string CertificateSha256);
sealed class SessionLease(ClientWebSocket socket, MockState state, string sessionId, byte[] secret) : IAsyncDisposable
{
    public ClientWebSocket Socket { get; } = socket;
    public MockState State { get; } = state;
    public string SessionId { get; } = sessionId;
    public byte[] Secret { get; } = secret;
    public async ValueTask DisposeAsync() { if (Socket.State == WebSocketState.Open) await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); Socket.Dispose(); }
}
