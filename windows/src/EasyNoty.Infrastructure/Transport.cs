using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using EasyNoty.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZstdSharp;

namespace EasyNoty.Infrastructure;

public sealed record ReceiverOptions(int Port = 38492, string PcDeviceId = "", string PcName = "EasyNoty PC");

public sealed class CertificateService
{
    public X509Certificate2 GetOrCreate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var current = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, "CN=EasyNoty Local Receiver", false)
            .OfType<X509Certificate2>().FirstOrDefault(x => x.NotAfter > DateTime.UtcNow.AddDays(30) && x.HasPrivateKey);
        if (current is not null) return current;
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=EasyNoty Local Receiver", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(3));
        var persisted = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        store.Add(persisted);
        return persisted;
    }

    public static string Fingerprint(X509Certificate2 certificate) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(certificate.RawData));
}

internal sealed class OrderedTransfer(long encodedSize, int chunkCount)
{
    private readonly MemoryStream _data = new();
    private int _nextIndex;
    public void Add(int index, long offset, string dataB64)
    {
        if (index != _nextIndex || offset != _data.Length) throw new InvalidDataException("Transfer chunks must be ordered and contiguous.");
        var bytes = Convert.FromBase64String(dataB64);
        if (bytes.Length > ProtocolV1.ChunkSize) throw new InvalidDataException("Chunk exceeds 65,536 decoded bytes.");
        if (_data.Length + bytes.Length > encodedSize) throw new InvalidDataException("Transfer exceeds declared encoded size.");
        _data.Write(bytes);
        _nextIndex++;
    }
    public byte[] Complete()
    {
        if (_nextIndex != chunkCount || _data.Length != encodedSize) throw new InvalidDataException("Transfer is incomplete.");
        return _data.ToArray();
    }
}

internal sealed record AssetTransferState(AssetTransferStart Start, OrderedTransfer Data);
internal sealed record EventTransferState(EventTransferStart Start, OrderedTransfer Data);
internal sealed record Outbound(string MessageType, object Payload);

public sealed class ReceiverWebSocketService(
    IServiceProvider root,
    ReceiverOptions options,
    CertificateService certificates,
    ILogger<ReceiverWebSocketService> logger) : IHostedService
{
    private static readonly HashSet<string> MessageTypes = ["hello", "pairing_request", "pairing_accept", "pairing_reject", "auth_challenge", "auth_response", "auth_success", "notification_event", "event_transfer_start", "event_transfer_chunk", "event_transfer_complete", "event_ack", "asset_missing_request", "asset_transfer_start", "asset_transfer_chunk", "asset_transfer_complete", "asset_ack", "ping", "pong", "error"];
    private WebApplication? _application;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var cert = certificates.GetOrCreate();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(server => server.ListenAnyIP(options.Port, listen => listen.UseHttps(cert)));
        var app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
        app.MapGet("/health", () => Results.Ok(new { service = "EasyNoty", protocol_version = ProtocolV1.Version, certificate_sha256 = CertificateService.Fingerprint(cert) }));
        app.Map("/easynoty/pair", HandlePairSocketAsync);
        app.Map("/easynoty/sync", HandleSyncSocketAsync);
        _application = app;
        await app.StartAsync(cancellationToken);
        logger.LogInformation("EasyNoty WSS receiver listening on port {Port}; certificate {Fingerprint}", options.Port, CertificateService.Fingerprint(cert));
    }

    private async Task HandlePairSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        try
        {
            var envelope = await ReceiveEnvelopeAsync(socket, context.RequestAborted);
            ValidatePreSession(envelope, "pairing_request");
            var request = envelope.Payload.Deserialize<PairingRequest>(ProtocolV1.Json) ?? throw new InvalidDataException("Pairing request is missing.");
            if (request.AndroidDeviceId != envelope.DeviceId) throw new InvalidDataException("Pairing device IDs do not match.");
            using var scope = root.CreateScope();
            var accepted = await scope.ServiceProvider.GetRequiredService<PairingService>().RequestApprovalAsync(request, context.RequestAborted);
            await SendEnvelopeAsync(socket, CreateEnvelope("pairing_accept", accepted, options.PcDeviceId, null, null, null), context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var code = ex is UnauthorizedAccessException && ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ? "expired_token" : ex is UnauthorizedAccessException ? "invalid_token" : "pairing_unavailable";
            await SendEnvelopeAsync(socket, CreateEnvelope("pairing_reject", new PairingReject(code, SafeMessage(ex), code == "pairing_unavailable"), options.PcDeviceId, null, null, null), context.RequestAborted);
        }
    }

    private async Task HandleSyncSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        DeviceCredential? credential = null;
        string? sessionId = null;
        try
        {
            var hello = await ReceiveEnvelopeAsync(socket, context.RequestAborted);
            ValidatePreSession(hello, "hello");
            _ = hello.Payload.Deserialize<HelloPayload>(ProtocolV1.Json) ?? throw new InvalidDataException("Hello payload is missing.");
            using var scope = root.CreateScope();
            var pairing = scope.ServiceProvider.GetRequiredService<PairingService>();
            credential = await pairing.GetCredentialAsync(hello.DeviceId, context.RequestAborted);
            var challengeId = WireValues.Uuid();
            var serverNonce = WireValues.Base64Url(RandomNumberGenerator.GetBytes(16));
            var challengeExpiry = DateTimeOffset.UtcNow.AddMinutes(2);
            await SendEnvelopeAsync(socket, CreateEnvelope("auth_challenge", new AuthChallenge(challengeId, serverNonce, WireValues.Utc(challengeExpiry)), options.PcDeviceId, null, null, null), context.RequestAborted);
            var responseEnvelope = await ReceiveEnvelopeAsync(socket, context.RequestAborted);
            ValidatePreSession(responseEnvelope, "auth_response");
            var response = responseEnvelope.Payload.Deserialize<AuthResponse>(ProtocolV1.Json) ?? throw new InvalidDataException("Auth response is missing.");
            if (DateTimeOffset.UtcNow > challengeExpiry || response.ChallengeId != challengeId || response.KeyId != credential.KeyId) throw new UnauthorizedAccessException("Authentication challenge is invalid or expired.");
            var expected = SessionAuthentication.Proof(credential.Secret, challengeId, serverNonce, response.ClientNonce, credential.DeviceId);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(response.Proof))) throw new UnauthorizedAccessException("Authentication proof failed.");
            sessionId = WireValues.Uuid();
            var authenticatedAt = DateTimeOffset.UtcNow;
            await SendEnvelopeAsync(socket, CreateEnvelope("auth_success", new AuthSuccess(sessionId, WireValues.Utc(authenticatedAt), WireValues.Utc(authenticatedAt.AddHours(12))), options.PcDeviceId, null, null, null), context.RequestAborted);
            await ProcessSessionAsync(socket, credential, sessionId, context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Sync session rejected: {ErrorType}", ex.GetType().Name);
            var error = new ProtocolError(ex is UnauthorizedAccessException ? "authentication_failed" : "invalid_payload", SafeMessage(ex), false, null);
            var envelope = CreateEnvelope("error", error, options.PcDeviceId, sessionId, credential?.KeyId, credential?.Secret);
            if (socket.State == WebSocketState.Open) await SendEnvelopeAsync(socket, envelope, context.RequestAborted);
        }
    }

    private async Task ProcessSessionAsync(WebSocket socket, DeviceCredential credential, string sessionId, CancellationToken ct)
    {
        var assets = new Dictionary<string, AssetTransferState>(StringComparer.Ordinal);
        var events = new Dictionary<string, EventTransferState>(StringComparer.Ordinal);
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            ProtocolEnvelope incoming;
            try { incoming = await ReceiveEnvelopeAsync(socket, ct); }
            catch (WebSocketException) { break; }
            ValidateAuthenticated(incoming, credential, sessionId);
            using var scope = root.CreateScope();
            var pairing = scope.ServiceProvider.GetRequiredService<PairingService>();
            var signedDigest = incoming.Authentication!.PayloadSha256 + ":" + incoming.Authentication.Signature;
            var replay = await pairing.CheckReplayAsync(incoming.MessageId, credential.DeviceId, sessionId, signedDigest, ct);
            if (replay.Disposition == ReplayDisposition.Conflict)
            {
                await SendSignedAsync(socket, credential, sessionId, new Outbound("error", new ProtocolError("replay_detected", "The message ID was reused with different signed content.", false, incoming.MessageId)), ct);
                continue;
            }
            if (replay is { Disposition: ReplayDisposition.Identical, ResponseJson: not null })
            {
                if (replay.ResponseJson.Length > 0) await SendRawAsync(socket, replay.ResponseJson, ct);
                continue;
            }

            IReadOnlyList<Outbound> outgoing;
            try { outgoing = await ProcessMessageAsync(scope.ServiceProvider, incoming, assets, events, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var code = ex is InvalidDataException && ex.Message.Contains("hash", StringComparison.OrdinalIgnoreCase) ? "hash_mismatch" : ex is InvalidDataException ? "invalid_payload" : "persistence_failed";
                outgoing = [new("error", new ProtocolError(code, SafeMessage(ex), code == "persistence_failed", incoming.MessageId))];
            }
            string? firstJson = null;
            foreach (var item in outgoing)
            {
                var envelope = CreateEnvelope(item.MessageType, item.Payload, options.PcDeviceId, sessionId, credential.KeyId, credential.Secret);
                var json = JsonSerializer.Serialize(envelope, ProtocolV1.Json);
                firstJson ??= json;
                await SendRawAsync(socket, json, ct);
            }
            await pairing.CacheResponseAsync(incoming.MessageId, credential.DeviceId, sessionId, signedDigest, firstJson ?? string.Empty, ct);
        }
    }

    private static async Task<IReadOnlyList<Outbound>> ProcessMessageAsync(IServiceProvider services, ProtocolEnvelope envelope, Dictionary<string, AssetTransferState> assets, Dictionary<string, EventTransferState> events, CancellationToken ct)
    {
        switch (envelope.MessageType)
        {
            case "ping":
                var ping = envelope.Payload.Deserialize<KeepalivePayload>(ProtocolV1.Json) ?? throw new InvalidDataException("Ping is missing.");
                return [new("pong", ping)];
            case "notification_event":
                return await PersistNotificationAsync(services, envelope.Payload.Deserialize<NotificationEventPayload>(ProtocolV1.Json) ?? throw new InvalidDataException("Notification payload is missing."), envelope.DeviceId, ct);
            case "event_transfer_start":
                var eventStart = envelope.Payload.Deserialize<EventTransferStart>(ProtocolV1.Json) ?? throw new InvalidDataException("Event transfer start is missing.");
                ValidateTransferSizes(eventStart.EncodedSize, eventStart.ChunkSize, eventStart.ChunkCount);
                events.Add(eventStart.TransferId, new(eventStart, new(eventStart.EncodedSize, eventStart.ChunkCount)));
                return [];
            case "event_transfer_chunk":
                var eventChunk = envelope.Payload.Deserialize<EventTransferChunk>(ProtocolV1.Json) ?? throw new InvalidDataException("Event chunk is missing.");
                if (!events.TryGetValue(eventChunk.TransferId, out var eventState)) throw new InvalidDataException("Unknown event transfer.");
                eventState.Data.Add(eventChunk.ChunkIndex, eventChunk.Offset, eventChunk.DataB64);
                return [];
            case "event_transfer_complete":
                var eventComplete = envelope.Payload.Deserialize<EventTransferComplete>(ProtocolV1.Json) ?? throw new InvalidDataException("Event completion is missing.");
                if (!events.Remove(eventComplete.TransferId, out var completedEvent)) throw new InvalidDataException("Unknown event transfer.");
                if (eventComplete.EventId != completedEvent.Start.EventId || eventComplete.EncodedSize != completedEvent.Start.EncodedSize || eventComplete.CanonicalSha256 != completedEvent.Start.CanonicalSha256) throw new InvalidDataException("Event completion metadata conflicts with start.");
                var canonical = Decode(completedEvent.Data.Complete(), completedEvent.Start.ContentEncoding);
                VerifyCanonical(canonical, completedEvent.Start.CanonicalSize, completedEvent.Start.CanonicalSha256);
                var payload = JsonSerializer.Deserialize<NotificationEventPayload>(canonical, ProtocolV1.Json) ?? throw new InvalidDataException("Framed notification is missing.");
                if (payload.EventId != completedEvent.Start.EventId) throw new InvalidDataException("Framed event ID does not match transfer.");
                return await PersistNotificationAsync(services, payload, envelope.DeviceId, ct);
            case "asset_transfer_start":
                var assetStart = envelope.Payload.Deserialize<AssetTransferStart>(ProtocolV1.Json) ?? throw new InvalidDataException("Asset transfer start is missing.");
                if (assetStart.CanonicalSize is < 1 or > ProtocolV1.MaximumAssetBytes || assetStart.EncodedSize is < 1 or > ProtocolV1.MaximumAssetBytes) throw new InvalidDataException("Asset size limit exceeded.");
                if (assetStart.ChunkCount == 0)
                {
                    if (assetStart.ChunkSize != 0 || assetStart.InlineDataB64 is null || assetStart.CanonicalSize >= ProtocolV1.ChunkSize) throw new InvalidDataException("Inline asset framing is invalid.");
                    var stored = await services.GetRequiredService<AssetStore>().StoreAsync(assetStart.AssetHash, assetStart.Mime, Convert.FromBase64String(assetStart.InlineDataB64), assetStart.ContentEncoding, ct);
                    if (stored.ByteSize != assetStart.CanonicalSize) throw new InvalidDataException("Asset canonical size mismatch.");
                    return [new("asset_ack", ToAck(stored))];
                }
                if (assetStart.InlineDataB64 is not null || assetStart.ChunkSize is < 1 or > ProtocolV1.ChunkSize || assetStart.CanonicalSize < ProtocolV1.ChunkSize) throw new InvalidDataException("Chunked asset framing is invalid.");
                assets.Add(assetStart.TransferId, new(assetStart, new(assetStart.EncodedSize, assetStart.ChunkCount)));
                return [];
            case "asset_transfer_chunk":
                var assetChunk = envelope.Payload.Deserialize<AssetTransferChunk>(ProtocolV1.Json) ?? throw new InvalidDataException("Asset chunk is missing.");
                if (!assets.TryGetValue(assetChunk.TransferId, out var assetState)) throw new InvalidDataException("Unknown asset transfer.");
                assetState.Data.Add(assetChunk.ChunkIndex, assetChunk.Offset, assetChunk.DataB64);
                return [];
            case "asset_transfer_complete":
                var assetComplete = envelope.Payload.Deserialize<AssetTransferComplete>(ProtocolV1.Json) ?? throw new InvalidDataException("Asset completion is missing.");
                if (!assets.Remove(assetComplete.TransferId, out var completedAsset)) throw new InvalidDataException("Unknown asset transfer.");
                if (assetComplete.AssetHash != completedAsset.Start.AssetHash || assetComplete.EncodedSize != completedAsset.Start.EncodedSize) throw new InvalidDataException("Asset completion metadata conflicts with start.");
                var assetResult = await services.GetRequiredService<AssetStore>().StoreAsync(completedAsset.Start.AssetHash, completedAsset.Start.Mime, completedAsset.Data.Complete(), completedAsset.Start.ContentEncoding, ct);
                if (assetResult.ByteSize != completedAsset.Start.CanonicalSize) throw new InvalidDataException("Asset canonical size mismatch.");
                return [new("asset_ack", ToAck(assetResult))];
            default: throw new InvalidDataException($"Message type '{envelope.MessageType}' is not accepted from Android in an authenticated session.");
        }
    }

    private static async Task<IReadOnlyList<Outbound>> PersistNotificationAsync(IServiceProvider services, NotificationEventPayload payload, string deviceId, CancellationToken ct)
    {
        var canonicalRaw = JcsCanonicalizer.Canonicalize(payload.RawSnapshot);
        VerifyCanonical(canonicalRaw, payload.RawSnapshotByteSize, payload.RawSnapshotSha256);
        if (payload.Assets.Count > 64) throw new InvalidDataException("Notification has more than 64 assets.");
        var result = await services.GetRequiredService<NotificationReceiver>().ReceiveAsync(payload.ToDomain(deviceId), ct);
        var ack = new EventAck(payload.EventId, "persisted", WireValues.Utc(result.Ack.PersistedAt), result.Ack.Duplicate);
        if (result.MissingAssetHashes.Count == 0) return [new("event_ack", ack)];
        return [new("event_ack", ack), new("asset_missing_request", new AssetMissingRequest(payload.EventId, result.MissingAssetHashes.Select(x => "sha256:" + Hashing.NormalizeSha256(x)).ToArray()))];
    }

    private static AssetAck ToAck(AssetStoreResult stored) => new("sha256:" + Hashing.NormalizeSha256(stored.Hash), "stored_verified", WireValues.Utc(stored.VerifiedAt), stored.Duplicate);
    private static void ValidateTransferSizes(long encodedSize, int chunkSize, int chunkCount)
    {
        if (encodedSize < 1 || chunkSize is < 1 or > ProtocolV1.ChunkSize || chunkCount < 1 || (long)chunkSize * chunkCount < encodedSize) throw new InvalidDataException("Transfer size/chunk metadata is invalid.");
    }
    private static void VerifyCanonical(byte[] canonical, long expectedSize, string expectedHash)
    {
        if (canonical.LongLength != expectedSize || "sha256:" + Hashing.Sha256Hex(canonical) != expectedHash) throw new InvalidDataException("Canonical size or SHA-256 hash mismatch.");
    }
    private static byte[] Decode(byte[] encoded, string encoding)
    {
        if (encoding == "none") return encoded;
        if (encoding == "zstd") { using var decompressor = new Decompressor(); return decompressor.Unwrap(encoded).ToArray(); }
        if (encoding == "zlib") { using var input = new MemoryStream(encoded); using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress); using var output = new MemoryStream(); zlib.CopyTo(output); return output.ToArray(); }
        throw new InvalidDataException("Unsupported content encoding.");
    }

    private static void ValidatePreSession(ProtocolEnvelope envelope, string expectedType)
    {
        ValidateEnvelopeBasics(envelope);
        if (envelope.MessageType != expectedType || envelope.SessionId is not null || envelope.Authentication is not null) throw new InvalidDataException($"Expected pre-session {expectedType} envelope.");
    }
    private static void ValidateAuthenticated(ProtocolEnvelope envelope, DeviceCredential credential, string sessionId)
    {
        ValidateEnvelopeBasics(envelope);
        if (envelope.DeviceId != credential.DeviceId || envelope.SessionId != sessionId || envelope.Authentication?.KeyId != credential.KeyId || !HmacAuthentication.Verify(envelope, credential.Secret)) throw new UnauthorizedAccessException("Envelope authentication failed.");
    }
    private static void ValidateEnvelopeBasics(ProtocolEnvelope envelope)
    {
        if (envelope.ProtocolVersion != ProtocolV1.Version) throw new InvalidDataException("Unsupported protocol version.");
        if (!MessageTypes.Contains(envelope.MessageType) || !Guid.TryParseExact(envelope.MessageId, "D", out _) || !Guid.TryParseExact(envelope.DeviceId, "D", out _)) throw new InvalidDataException("Envelope identity or message type is invalid.");
        if (!envelope.SentAtUtc.EndsWith('Z') || !DateTimeOffset.TryParse(envelope.SentAtUtc, out _)) throw new InvalidDataException("Envelope timestamp must be UTC RFC 3339.");
    }

    private ProtocolEnvelope CreateEnvelope(string type, object payload, string deviceId, string? sessionId, string? keyId, byte[]? secret)
    {
        var envelope = new ProtocolEnvelope(ProtocolV1.Version, WireValues.Uuid(), type, deviceId, WireValues.Utc(DateTimeOffset.UtcNow), sessionId, JsonSerializer.SerializeToElement(payload, ProtocolV1.Json), null);
        return secret is null || keyId is null ? envelope : envelope with { Authentication = HmacAuthentication.Sign(envelope, keyId, secret) };
    }
    private static async Task<ProtocolEnvelope> ReceiveEnvelopeAsync(WebSocket socket, CancellationToken ct)
    {
        var json = await ReadTextAsync(socket, ct) ?? throw new WebSocketException("Socket closed.");
        return JsonSerializer.Deserialize<ProtocolEnvelope>(json, ProtocolV1.Json) ?? throw new InvalidDataException("Envelope is empty.");
    }
    private static async Task<string?> ReadTextAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[128 * 1024];
        using var output = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct); return null; }
            if (result.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("Protocol messages must be JSON text frames.");
            output.Write(buffer, 0, result.Count);
            if (output.Length > ProtocolV1.LargePayloadThreshold + ProtocolV1.ChunkSize) throw new InvalidDataException("Inline envelope exceeds the 2 MiB boundary.");
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(output.ToArray());
    }
    private Task SendSignedAsync(WebSocket socket, DeviceCredential credential, string sessionId, Outbound outbound, CancellationToken ct)
    {
        var envelope = new ProtocolEnvelope(1, WireValues.Uuid(), outbound.MessageType, options.PcDeviceId, WireValues.Utc(DateTimeOffset.UtcNow), sessionId, JsonSerializer.SerializeToElement(outbound.Payload, ProtocolV1.Json), null);
        return SendEnvelopeAsync(socket, envelope with { Authentication = HmacAuthentication.Sign(envelope, credential.KeyId, credential.Secret) }, ct);
    }
    private static Task SendEnvelopeAsync(WebSocket socket, ProtocolEnvelope envelope, CancellationToken ct) => SendRawAsync(socket, JsonSerializer.Serialize(envelope, ProtocolV1.Json), ct);
    private static Task SendRawAsync(WebSocket socket, string json, CancellationToken ct) => socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    private static string SafeMessage(Exception ex) => ex.Message.Length <= 512 ? ex.Message : ex.Message[..512];
    public async Task StopAsync(CancellationToken cancellationToken) { if (_application is not null) await _application.StopAsync(cancellationToken); }
}

public sealed class RetentionBackgroundService(RetentionService retention, ILogger<RetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { var result = await retention.ApplyAsync(new(), DateTimeOffset.UtcNow, stoppingToken); logger.LogInformation("Retention archived {Archived} and soft-deleted {Deleted} notifications", result.Archived, result.Deleted); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Retention job failed"); }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
