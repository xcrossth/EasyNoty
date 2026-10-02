using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyNoty.Core;

public static class ProtocolV1
{
    public const int Version = 1;
    public const int ChunkSize = 64 * 1024;
    public const int LargePayloadThreshold = 2 * 1024 * 1024;
    public const int MaximumAssetBytes = 25 * 1024 * 1024;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public sealed record MessageAuthentication(string Algorithm, string KeyId, string PayloadSha256, string Signature);

public sealed record ProtocolEnvelope(
    int ProtocolVersion,
    string MessageId,
    string MessageType,
    string DeviceId,
    string SentAtUtc,
    string? SessionId,
    JsonElement Payload,
    MessageAuthentication? Authentication);

public sealed record ParsedNotification(
    string ParserName,
    int ParserVersion,
    string? Title,
    string? Body,
    string? PreviewText,
    string? SenderName,
    string? ConversationTitle,
    string? GroupingKey,
    double Confidence);

public sealed record AssetManifestItem(
    string Role,
    string Hash,
    string Mime,
    long OriginalSize,
    int? Width = null,
    int? Height = null);

public sealed record AssetManifestPayload(
    string Role,
    string Hash,
    string Mime,
    long OriginalSize,
    int? Width,
    int? Height,
    string SourceKind,
    string OriginalityLevel);

public sealed record ParsedResultPayload(
    string ParserName,
    int ParserVersion,
    string? Title,
    string? Body,
    string? SenderName,
    string? ConversationTitle,
    string? PreviewText,
    double Confidence);

public sealed record NotificationEventPayload(
    string EventId,
    string NotificationInstanceId,
    int RevisionIndex,
    string AndroidNotificationKey,
    string PackageName,
    string AppName,
    string PostedAtUtc,
    string CapturedAtUtc,
    int RawSnapshotSchemaVersion,
    long RawSnapshotByteSize,
    string RawSnapshotSha256,
    JsonElement RawSnapshot,
    ParsedResultPayload ParsedResult,
    IReadOnlyList<AssetManifestPayload> Assets,
    string Style,
    bool IsOngoing,
    bool IsGroupSummary,
    string? GroupKey = null,
    string? SortKey = null,
    string? MinimalCaptureError = null)
{
    public NotificationEvent ToDomain(string deviceId) => new(
        Guid.Parse(EventId), NotificationInstanceId, RevisionIndex, AndroidNotificationKey, deviceId, PackageName, AppName,
        DateTimeOffset.Parse(PostedAtUtc, System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.Parse(CapturedAtUtc, System.Globalization.CultureInfo.InvariantCulture),
        RawSnapshotSchemaVersion, JcsCanonicalizer.CanonicalizeToString(RawSnapshot), RawSnapshotByteSize, RawSnapshotSha256,
        IsOngoing, IsGroupSummary, Style, GroupKey,
        new(ParsedResult.ParserName, ParsedResult.ParserVersion, ParsedResult.Title, ParsedResult.Body, ParsedResult.PreviewText, ParsedResult.SenderName, ParsedResult.ConversationTitle, GroupKey, ParsedResult.Confidence),
        Assets.Select(x => new AssetManifestItem(x.Role, x.Hash, x.Mime, x.OriginalSize, x.Width, x.Height)).ToArray());
}

public sealed record NotificationEvent(
    Guid EventId,
    string NotificationInstanceId,
    int RevisionIndex,
    string AndroidNotificationKey,
    string DeviceId,
    string PackageName,
    string AppName,
    DateTimeOffset PostedAt,
    DateTimeOffset CapturedAt,
    int RawSnapshotSchemaVersion,
    string RawSnapshotJson,
    long RawByteSize,
    string RawSha256,
    bool IsOngoing,
    bool IsGroupSummary,
    string? NotificationStyle,
    string? GroupKey,
    ParsedNotification? AndroidParsedResult,
    IReadOnlyList<AssetManifestItem> Assets);

public sealed record PairingEndpoint(string Host, int Port);
public sealed record PairingQr(int ProtocolVersion, string PcDeviceId, string PcName, IReadOnlyList<PairingEndpoint> Endpoints, string CertificateSha256, string PairingToken, string ExpiresAtUtc);
public sealed record PairingRequest(string PairingToken, string AndroidDeviceId, string AndroidName, string ClientNonce, IReadOnlyList<string> Capabilities);
public sealed record PairingAccept(string PcDeviceId, string PcName, string KeyId, string PairingSecret, string VerificationCode, string ServerNonce, string PairedAtUtc);
public sealed record PairingReject(string Code, string Message, bool Retryable);
public sealed record HelloPayload(string Role, string DeviceName, IReadOnlyList<int> SupportedProtocolVersions, IReadOnlyList<string> Capabilities);
public sealed record AuthChallenge(string ChallengeId, string ServerNonce, string ExpiresAtUtc);
public sealed record AuthResponse(string ChallengeId, string KeyId, string ClientNonce, string Proof);
public sealed record AuthSuccess(string SessionId, string AuthenticatedAtUtc, string ExpiresAtUtc);
public sealed record KeepalivePayload(string Nonce);
public sealed record EventAck(string EventId, string Status, string PersistedAtUtc, bool Duplicate);
public sealed record AssetAck(string AssetHash, string Status, string VerifiedAtUtc, bool Duplicate);
public sealed record ProtocolError(string Code, string Message, bool Retryable, string? RelatedMessageId, IReadOnlyDictionary<string, object?>? Details = null);
public sealed record AssetMissingRequest(string EventId, IReadOnlyList<string> Hashes);
public sealed record AssetTransferStart(string TransferId, string AssetHash, string Mime, string ContentEncoding, long CanonicalSize, long EncodedSize, int ChunkSize, int ChunkCount, string? InlineDataB64);
public sealed record AssetTransferChunk(string TransferId, int ChunkIndex, long Offset, string DataB64);
public sealed record AssetTransferComplete(string TransferId, string AssetHash, long EncodedSize);
public sealed record EventTransferStart(string TransferId, string EventId, string ContentType, string ContentEncoding, long CanonicalSize, long EncodedSize, string CanonicalSha256, int ChunkSize, int ChunkCount);
public sealed record EventTransferChunk(string TransferId, int ChunkIndex, long Offset, string DataB64);
public sealed record EventTransferComplete(string TransferId, string EventId, long EncodedSize, string CanonicalSha256);
