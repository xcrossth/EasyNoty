using System.Text;
using EasyNoty.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyNoty.Infrastructure;

public sealed class DeviceService(IDbContextFactory<EasyNotyDbContext> dbFactory)
{
    public async Task<IReadOnlyList<DeviceEntity>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Devices.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(ct);
    }
    public Task RenameAsync(string deviceId, string displayName, CancellationToken ct = default) => UpdateAsync(deviceId, d => d.DisplayName = string.IsNullOrWhiteSpace(displayName) ? throw new ArgumentException("Device name is required.") : displayName.Trim(), ct);
    public Task SetPausedAsync(string deviceId, bool paused, CancellationToken ct = default) => UpdateAsync(deviceId, d => d.IsPaused = paused, ct);
    public Task RevokeAsync(string deviceId, CancellationToken ct = default) => UpdateAsync(deviceId, d => { d.IsRevoked = true; d.IsPaused = true; d.SharedSecretRef = null; d.KeyId = null; }, ct);
    private async Task UpdateAsync(string deviceId, Action<DeviceEntity> update, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var device = await db.Devices.SingleAsync(x => x.DeviceId == deviceId, ct);
        update(device);
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ReparseService(IDbContextFactory<EasyNotyDbContext> dbFactory, NotificationParserEngine parser)
{
    public async Task<ParsedNotification> ReparseAsync(Guid notificationId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var notification = await db.Notifications.SingleAsync(x => x.Id == notificationId, ct);
        var revision = await db.NotificationRevisions.Where(x => x.NotificationId == notificationId).OrderByDescending(x => x.RevisionIndex).FirstAsync(ct);
        var app = await db.Apps.SingleAsync(x => x.PackageName == notification.PackageName, ct);
        var parsed = parser.Parse(new NotificationEvent(revision.EventId, notification.NotificationInstanceId, revision.RevisionIndex, notification.AndroidNotificationKey, notification.DeviceId, notification.PackageName, app.AppName, notification.PostedAt, revision.CapturedAt,
            revision.RawSnapshotSchemaVersion, revision.RawSnapshotJson, revision.RawByteSize, revision.RawSha256, notification.IsOngoing, notification.IsGroupSummary, notification.NotificationStyle, notification.GroupKey, null, []));
        var now = DateTimeOffset.UtcNow;
        foreach (var old in await db.NotificationParsedResults.Where(x => x.NotificationId == notificationId && x.IsCurrent).ToListAsync(ct)) old.IsCurrent = false;
        db.NotificationParsedResults.Add(new NotificationParsedResultEntity { NotificationId = notificationId, ParserName = parsed.ParserName, ParserVersion = parsed.ParserVersion, ParsedJson = System.Text.Json.JsonSerializer.Serialize(parsed, ProtocolV1.Json), Title = parsed.Title, Body = parsed.Body, PreviewText = parsed.PreviewText, SenderName = parsed.SenderName, ConversationTitle = parsed.ConversationTitle, GroupingKey = parsed.GroupingKey, Confidence = parsed.Confidence, ParsedAt = now, IsCurrent = true });
        notification.CurrentParserName = parsed.ParserName; notification.CurrentParserVersion = parsed.ParserVersion; notification.Title = parsed.Title; notification.Body = parsed.Body; notification.PreviewText = parsed.PreviewText; notification.SenderName = parsed.SenderName; notification.ConversationTitle = parsed.ConversationTitle; notification.GroupingKey = parsed.GroupingKey; notification.Confidence = parsed.Confidence; notification.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return parsed;
    }

    public async Task<string> GetRawAsync(Guid notificationId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.NotificationRevisions.Where(x => x.NotificationId == notificationId).OrderByDescending(x => x.RevisionIndex).Select(x => x.RawSnapshotJson).FirstAsync(ct);
    }
}

public sealed record StorageMigrationResult(string Destination, string SourceBackup, bool RestartRequired);

public sealed class StorageMigrationService(StoragePaths current, IDbContextFactory<EasyNotyDbContext> dbFactory)
{
    public async Task<StorageMigrationResult> CopyAndVerifyAsync(string destinationRoot, Func<Task> stopReceiver, CancellationToken ct = default)
    {
        destinationRoot = Path.GetFullPath(destinationRoot);
        if (destinationRoot.StartsWith(Path.GetFullPath(current.Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Destination cannot be inside the current storage root.");
        await stopReceiver();
        await using (var db = await dbFactory.CreateDbContextAsync(ct)) await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);", ct);
        SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(destinationRoot);
        foreach (var source in Directory.EnumerateFiles(current.Root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(current.Root, source);
            var destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
            if (!await SameFileAsync(source, destination, ct)) throw new IOException($"Storage verification failed for '{relative}'.");
        }
        var backup = current.Root.TrimEnd(Path.DirectorySeparatorChar) + ".backup-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        Directory.Move(current.Root, backup);
        return new(destinationRoot, backup, true);
    }

    private static async Task<bool> SameFileAsync(string first, string second, CancellationToken ct)
    {
        if (new FileInfo(first).Length != new FileInfo(second).Length) return false;
        await using var a = File.OpenRead(first); await using var b = File.OpenRead(second);
        var ah = await System.Security.Cryptography.SHA256.HashDataAsync(a, ct); var bh = await System.Security.Cryptography.SHA256.HashDataAsync(b, ct);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ah, bh);
    }
}
