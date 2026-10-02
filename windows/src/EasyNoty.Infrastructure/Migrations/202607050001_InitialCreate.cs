using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace EasyNoty.Infrastructure.Migrations;

[DbContext(typeof(EasyNotyDbContext))]
[Migration("202607050001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
CREATE TABLE IF NOT EXISTS Devices (Id TEXT NOT NULL PRIMARY KEY, DeviceId TEXT NOT NULL, DisplayName TEXT NOT NULL, DeviceType TEXT NOT NULL, DeviceImageAssetHash TEXT NULL, PublicKey TEXT NULL, SharedSecretRef TEXT NULL, KeyId TEXT NULL, PairedAt TEXT NOT NULL, LastSeenAt TEXT NULL, IsPaused INTEGER NOT NULL, IsRevoked INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Devices_DeviceId ON Devices(DeviceId);
CREATE TABLE IF NOT EXISTS Apps (Id TEXT NOT NULL PRIMARY KEY, PackageName TEXT NOT NULL, AppName TEXT NOT NULL, AppIconAssetHash TEXT NULL, IsSensitive INTEGER NOT NULL, FirstSeenAt TEXT NOT NULL, LastSeenAt TEXT NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Apps_PackageName ON Apps(PackageName);
CREATE TABLE IF NOT EXISTS Notifications (Id TEXT NOT NULL PRIMARY KEY, NotificationInstanceId TEXT NOT NULL, DeviceId TEXT NOT NULL, PackageName TEXT NOT NULL, AndroidNotificationKey TEXT NOT NULL, PostedAt TEXT NOT NULL, CapturedAt TEXT NOT NULL, ReceivedAt TEXT NOT NULL, LatestRevisionIndex INTEGER NOT NULL, IsOngoing INTEGER NOT NULL, IsGroupSummary INTEGER NOT NULL, NotificationStyle TEXT NULL, GroupKey TEXT NULL, CurrentParserName TEXT NOT NULL, CurrentParserVersion INTEGER NOT NULL, Title TEXT NULL, Body TEXT NULL, PreviewText TEXT NULL, SenderName TEXT NULL, ConversationTitle TEXT NULL, GroupingKey TEXT NULL, Confidence REAL NOT NULL, AppIconAssetHash TEXT NULL, AvatarAssetHash TEXT NULL, PreviewAssetHash TEXT NULL, IsRead INTEGER NOT NULL, ReadAt TEXT NULL, IsArchived INTEGER NOT NULL, ArchivedAt TEXT NULL, IsDeleted INTEGER NOT NULL, DeletedAt TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Notifications_DeviceId_NotificationInstanceId ON Notifications(DeviceId, NotificationInstanceId);
CREATE INDEX IF NOT EXISTS IX_Notifications_PostedAt_IsDeleted_IsArchived ON Notifications(PostedAt, IsDeleted, IsArchived);
CREATE TABLE IF NOT EXISTS NotificationRevisions (Id TEXT NOT NULL PRIMARY KEY, NotificationId TEXT NOT NULL, EventId TEXT NOT NULL, DeviceId TEXT NOT NULL, RevisionIndex INTEGER NOT NULL, RawSnapshotSchemaVersion INTEGER NOT NULL, RawSnapshotJson TEXT NOT NULL, RawByteSize INTEGER NOT NULL, RawSha256 TEXT NOT NULL, IsOversized INTEGER NOT NULL, CaptureError TEXT NULL, CapturedAt TEXT NOT NULL, CreatedAt TEXT NOT NULL, FOREIGN KEY(NotificationId) REFERENCES Notifications(Id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS IX_NotificationRevisions_DeviceId_EventId ON NotificationRevisions(DeviceId, EventId);
CREATE UNIQUE INDEX IF NOT EXISTS IX_NotificationRevisions_NotificationId_RevisionIndex ON NotificationRevisions(NotificationId, RevisionIndex);
CREATE TABLE IF NOT EXISTS NotificationParsedResults (Id TEXT NOT NULL PRIMARY KEY, NotificationId TEXT NOT NULL, ParserName TEXT NOT NULL, ParserVersion INTEGER NOT NULL, ParsedJson TEXT NOT NULL, Title TEXT NULL, Body TEXT NULL, PreviewText TEXT NULL, SenderName TEXT NULL, ConversationTitle TEXT NULL, GroupingKey TEXT NULL, Confidence REAL NOT NULL, ParsedAt TEXT NOT NULL, IsCurrent INTEGER NOT NULL, FOREIGN KEY(NotificationId) REFERENCES Notifications(Id) ON DELETE RESTRICT);
CREATE INDEX IF NOT EXISTS IX_NotificationParsedResults_NotificationId_IsCurrent ON NotificationParsedResults(NotificationId, IsCurrent);
CREATE TABLE IF NOT EXISTS Assets (Hash TEXT NOT NULL PRIMARY KEY, Mime TEXT NOT NULL, ByteSize INTEGER NOT NULL, Width INTEGER NULL, Height INTEGER NULL, LocalPath TEXT NOT NULL, SourceKind TEXT NOT NULL, OriginalityLevel TEXT NOT NULL, FirstSeenAt TEXT NOT NULL, LastSeenAt TEXT NOT NULL, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS NotificationAssets (Id TEXT NOT NULL PRIMARY KEY, NotificationId TEXT NOT NULL, AssetHash TEXT NOT NULL, Role TEXT NOT NULL, CreatedAt TEXT NOT NULL, FOREIGN KEY(NotificationId) REFERENCES Notifications(Id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS IX_NotificationAssets_NotificationId_AssetHash_Role ON NotificationAssets(NotificationId, AssetHash, Role);
CREATE TABLE IF NOT EXISTS PairingSessions (Id TEXT NOT NULL PRIMARY KEY, TokenHash TEXT NOT NULL, PcDeviceId TEXT NOT NULL, PcName TEXT NOT NULL, ExpiresAt TEXT NOT NULL, CreatedAt TEXT NOT NULL, ConsumedAt TEXT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS IX_PairingSessions_TokenHash ON PairingSessions(TokenHash);
CREATE TABLE IF NOT EXISTS ProcessedMessages (MessageId TEXT NOT NULL, DeviceId TEXT NOT NULL, SessionId TEXT NOT NULL, SignedDigest TEXT NOT NULL, ResponseJson TEXT NOT NULL, ProcessedAt TEXT NOT NULL, PRIMARY KEY(DeviceId, SessionId, MessageId));
CREATE TABLE IF NOT EXISTS Settings (Key TEXT NOT NULL PRIMARY KEY, ValueJson TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TABLE IF EXISTS NotificationAssets; DROP TABLE IF EXISTS NotificationParsedResults; DROP TABLE IF EXISTS NotificationRevisions; DROP TABLE IF EXISTS ProcessedMessages; DROP TABLE IF EXISTS PairingSessions; DROP TABLE IF EXISTS Settings; DROP TABLE IF EXISTS Assets; DROP TABLE IF EXISTS Notifications; DROP TABLE IF EXISTS Apps; DROP TABLE IF EXISTS Devices;");
    }
}
