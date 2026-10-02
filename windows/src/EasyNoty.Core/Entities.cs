namespace EasyNoty.Core;

public sealed class DeviceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DeviceId { get; set; }
    public required string DisplayName { get; set; }
    public string DeviceType { get; set; } = "android";
    public string? DeviceImageAssetHash { get; set; }
    public string? PublicKey { get; set; }
    public string? SharedSecretRef { get; set; }
    public string? KeyId { get; set; }
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsPaused { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AppEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string PackageName { get; set; }
    public required string AppName { get; set; }
    public string? AppIconAssetHash { get; set; }
    public bool IsSensitive { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NotificationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string NotificationInstanceId { get; set; }
    public required string DeviceId { get; set; }
    public required string PackageName { get; set; }
    public required string AndroidNotificationKey { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public int LatestRevisionIndex { get; set; }
    public bool IsOngoing { get; set; }
    public bool IsGroupSummary { get; set; }
    public string? NotificationStyle { get; set; }
    public string? GroupKey { get; set; }
    public string CurrentParserName { get; set; } = "generic_android";
    public int CurrentParserVersion { get; set; } = 1;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? PreviewText { get; set; }
    public string? SenderName { get; set; }
    public string? ConversationTitle { get; set; }
    public string? GroupingKey { get; set; }
    public double Confidence { get; set; }
    public string? AppIconAssetHash { get; set; }
    public string? AvatarAssetHash { get; set; }
    public string? PreviewAssetHash { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<NotificationRevisionEntity> Revisions { get; set; } = [];
    public List<NotificationParsedResultEntity> ParsedResults { get; set; } = [];
    public List<NotificationAssetEntity> AssetMappings { get; set; } = [];
}

public sealed class NotificationRevisionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public Guid EventId { get; set; }
    public required string DeviceId { get; set; }
    public int RevisionIndex { get; set; }
    public int RawSnapshotSchemaVersion { get; set; }
    public required string RawSnapshotJson { get; set; }
    public long RawByteSize { get; set; }
    public required string RawSha256 { get; set; }
    public bool IsOversized { get; set; }
    public string? CaptureError { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public NotificationEntity? Notification { get; set; }
}

public sealed class NotificationParsedResultEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public required string ParserName { get; set; }
    public int ParserVersion { get; set; }
    public required string ParsedJson { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? PreviewText { get; set; }
    public string? SenderName { get; set; }
    public string? ConversationTitle { get; set; }
    public string? GroupingKey { get; set; }
    public double Confidence { get; set; }
    public DateTimeOffset ParsedAt { get; set; }
    public bool IsCurrent { get; set; }
    public NotificationEntity? Notification { get; set; }
}

public sealed class AssetEntity
{
    public required string Hash { get; set; }
    public required string Mime { get; set; }
    public long ByteSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public required string LocalPath { get; set; }
    public string SourceKind { get; set; } = "android";
    public string OriginalityLevel { get; set; } = "canonical";
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class NotificationAssetEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public required string AssetHash { get; set; }
    public required string Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public NotificationEntity? Notification { get; set; }
    public AssetEntity? Asset { get; set; }
}

public sealed class PairingSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TokenHash { get; set; }
    public required string PcDeviceId { get; set; }
    public required string PcName { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class ProcessedMessageEntity
{
    public Guid MessageId { get; set; }
    public required string DeviceId { get; set; }
    public required string SessionId { get; set; }
    public required string SignedDigest { get; set; }
    public required string ResponseJson { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class SettingEntity
{
    public required string Key { get; set; }
    public required string ValueJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
