using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyNoty.Core;
using Microsoft.EntityFrameworkCore;

namespace EasyNoty.Infrastructure;

public interface IToastNotifier
{
    Task ShowAsync(Guid notificationId, string appName, string? title, string? body, bool redacted, CancellationToken ct = default);
}

public sealed class NullToastNotifier : IToastNotifier
{
    public Task ShowAsync(Guid notificationId, string appName, string? title, string? body, bool redacted, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed record PersistenceAck(Guid EventId, Guid NotificationId, int RevisionIndex, bool Duplicate, DateTimeOffset PersistedAt);
public sealed record ReceiveResult(PersistenceAck Ack, IReadOnlyList<string> MissingAssetHashes, bool SuppressPresentation);

public sealed class NotificationReceiver(
    IDbContextFactory<EasyNotyDbContext> dbFactory,
    NotificationParserEngine parser,
    IToastNotifier toast)
{
    public async Task<ReceiveResult> ReceiveAsync(NotificationEvent incoming, CancellationToken ct = default)
    {
        Validate(incoming);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var duplicate = await db.NotificationRevisions.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceId == incoming.DeviceId && x.EventId == incoming.EventId, ct);
        if (duplicate is not null)
        {
            await tx.RollbackAsync(ct);
            return new(new(incoming.EventId, duplicate.NotificationId, duplicate.RevisionIndex, true, duplicate.CreatedAt), [], false);
        }

        var now = DateTimeOffset.UtcNow;
        var device = await db.Devices.SingleOrDefaultAsync(x => x.DeviceId == incoming.DeviceId, ct);
        if (device is null)
        {
            device = new DeviceEntity { DeviceId = incoming.DeviceId, DisplayName = incoming.DeviceId, PairedAt = now, CreatedAt = now, UpdatedAt = now };
            db.Devices.Add(device);
        }
        device.LastSeenAt = now;
        device.UpdatedAt = now;
        if (device.IsRevoked) throw new UnauthorizedAccessException("Device is revoked.");
        if (device.IsPaused) throw new InvalidOperationException("Device is paused.");

        var app = await db.Apps.SingleOrDefaultAsync(x => x.PackageName == incoming.PackageName, ct);
        if (app is null)
        {
            app = new AppEntity { PackageName = incoming.PackageName, AppName = incoming.AppName, FirstSeenAt = now, LastSeenAt = now, CreatedAt = now, UpdatedAt = now };
            db.Apps.Add(app);
        }
        app.AppName = incoming.AppName;
        app.LastSeenAt = now;
        app.UpdatedAt = now;

        NotificationEntity? notification = null;
        if (incoming.IsOngoing)
            notification = await db.Notifications.SingleOrDefaultAsync(x => x.DeviceId == incoming.DeviceId && x.NotificationInstanceId == incoming.NotificationInstanceId && x.IsOngoing, ct);

        var parsed = parser.Parse(incoming);
        if (notification is null)
        {
            notification = new NotificationEntity
            {
                NotificationInstanceId = incoming.NotificationInstanceId,
                DeviceId = incoming.DeviceId,
                PackageName = incoming.PackageName,
                AndroidNotificationKey = incoming.AndroidNotificationKey,
                PostedAt = incoming.PostedAt,
                CapturedAt = incoming.CapturedAt,
                ReceivedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Notifications.Add(notification);
        }

        ApplyCurrent(notification, incoming, parsed, now);
        db.NotificationRevisions.Add(new NotificationRevisionEntity
        {
            NotificationId = notification.Id,
            EventId = incoming.EventId,
            DeviceId = incoming.DeviceId,
            RevisionIndex = incoming.RevisionIndex,
            RawSnapshotSchemaVersion = incoming.RawSnapshotSchemaVersion,
            RawSnapshotJson = incoming.RawSnapshotJson,
            RawByteSize = incoming.RawByteSize,
            RawSha256 = Hashing.NormalizeSha256(incoming.RawSha256),
            IsOversized = incoming.RawByteSize > ProtocolV1.LargePayloadThreshold,
            CapturedAt = incoming.CapturedAt,
            CreatedAt = now
        });
        foreach (var old in await db.NotificationParsedResults.Where(x => x.NotificationId == notification.Id && x.IsCurrent).ToListAsync(ct)) old.IsCurrent = false;
        db.NotificationParsedResults.Add(new NotificationParsedResultEntity
        {
            NotificationId = notification.Id,
            ParserName = parsed.ParserName,
            ParserVersion = parsed.ParserVersion,
            ParsedJson = JsonSerializer.Serialize(parsed, ProtocolV1.Json),
            Title = parsed.Title,
            Body = parsed.Body,
            PreviewText = parsed.PreviewText,
            SenderName = parsed.SenderName,
            ConversationTitle = parsed.ConversationTitle,
            GroupingKey = parsed.GroupingKey,
            Confidence = parsed.Confidence,
            ParsedAt = now,
            IsCurrent = true
        });

        var normalizedAssets = incoming.Assets.Select(x => Hashing.NormalizeSha256(x.Hash)).Distinct().ToArray();
        var available = await db.Assets.Where(x => normalizedAssets.Contains(x.Hash)).Select(x => x.Hash).ToListAsync(ct);
        foreach (var manifest in incoming.Assets)
            db.NotificationAssets.Add(new NotificationAssetEntity { NotificationId = notification.Id, AssetHash = Hashing.NormalizeSha256(manifest.Hash), Role = manifest.Role, CreatedAt = now });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var missing = normalizedAssets.Except(available).ToArray();
        if (!incoming.IsGroupSummary)
            await toast.ShowAsync(notification.Id, incoming.AppName, parsed.Title, parsed.Body, app.IsSensitive, ct);
        return new(new(incoming.EventId, notification.Id, incoming.RevisionIndex, false, now), missing, incoming.IsGroupSummary);
    }

    private static void Validate(NotificationEvent n)
    {
        if (n.EventId == Guid.Empty || string.IsNullOrWhiteSpace(n.DeviceId) || string.IsNullOrWhiteSpace(n.PackageName)) throw new ArgumentException("Event identity is incomplete.");
        if (n.RevisionIndex < 0) throw new ArgumentOutOfRangeException(nameof(n.RevisionIndex));
        var bytes = Encoding.UTF8.GetBytes(n.RawSnapshotJson);
        if (!string.Equals(Hashing.Sha256Hex(bytes), Hashing.NormalizeSha256(n.RawSha256), StringComparison.Ordinal)) throw new InvalidDataException("Raw snapshot SHA-256 mismatch.");
    }

    private static void ApplyCurrent(NotificationEntity n, NotificationEvent incoming, ParsedNotification parsed, DateTimeOffset now)
    {
        n.LatestRevisionIndex = incoming.RevisionIndex;
        n.PostedAt = incoming.PostedAt;
        n.CapturedAt = incoming.CapturedAt;
        n.IsOngoing = incoming.IsOngoing;
        n.IsGroupSummary = incoming.IsGroupSummary;
        n.NotificationStyle = incoming.NotificationStyle;
        n.GroupKey = incoming.GroupKey;
        n.CurrentParserName = parsed.ParserName;
        n.CurrentParserVersion = parsed.ParserVersion;
        n.Title = parsed.Title;
        n.Body = parsed.Body;
        n.PreviewText = parsed.PreviewText;
        n.SenderName = parsed.SenderName;
        n.ConversationTitle = parsed.ConversationTitle;
        n.GroupingKey = parsed.GroupingKey;
        n.Confidence = parsed.Confidence;
        n.UpdatedAt = now;
        n.IsRead = false;
        n.ReadAt = null;
        foreach (var a in incoming.Assets)
        {
            var hash = Hashing.NormalizeSha256(a.Hash);
            if (a.Role == "app_icon") n.AppIconAssetHash = hash;
            else if (a.Role == "avatar") n.AvatarAssetHash = hash;
            else if (a.Role is "preview_image" or "big_picture") n.PreviewAssetHash = hash;
        }
    }
}

public sealed class InboxService(IDbContextFactory<EasyNotyDbContext> dbFactory, SensitiveRevealSession reveals)
{
    public async Task<IReadOnlyList<InboxItem>> QueryAsync(InboxQuery request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = from n in db.Notifications.AsNoTracking()
                    join a in db.Apps.AsNoTracking() on n.PackageName equals a.PackageName
                    where !n.IsDeleted && !n.IsGroupSummary
                    select new { n, a };
        query = request.View switch
        {
            InboxView.Inbox => query.Where(x => !x.n.IsArchived),
            InboxView.Archived => query.Where(x => x.n.IsArchived),
            _ => query
        };
        if (request.DeviceIds is { Count: > 0 }) query = query.Where(x => request.DeviceIds.Contains(x.n.DeviceId));
        if (request.PackageNames is { Count: > 0 }) query = query.Where(x => request.PackageNames.Contains(x.n.PackageName));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.a.AppName.Contains(term) || (x.n.Title != null && x.n.Title.Contains(term)) || (x.n.Body != null && x.n.Body.Contains(term)) || (x.n.SenderName != null && x.n.SenderName.Contains(term)));
        }
        var rows = await query.OrderByDescending(x => x.n.PostedAt).Skip(Math.Max(0, request.Skip)).Take(Math.Clamp(request.Take, 1, 500))
            .Select(x => new { x.n.Id, x.n.DeviceId, x.n.PackageName, x.a.AppName, x.n.PostedAt, x.n.Title, x.n.PreviewText, x.a.IsSensitive, x.n.IsRead, x.n.IsArchived, x.n.LatestRevisionIndex, Oversized = x.n.Revisions.Any(r => r.IsOversized) }).ToListAsync(ct);
        return rows.Select(x =>
        {
            var shown = !x.IsSensitive || reveals.IsRevealed(x.Id);
            return new InboxItem(x.Id, x.DeviceId, x.PackageName, x.AppName, x.PostedAt, shown ? x.Title : "Content hidden", shown ? x.PreviewText : "Click to reveal.", x.IsSensitive, shown && x.IsSensitive, x.IsRead, x.IsArchived, x.LatestRevisionIndex, x.Oversized);
        }).ToArray();
    }

    public async Task SetReadAsync(Guid id, bool value, CancellationToken ct = default) => await UpdateAsync(id, n => { n.IsRead = value; n.ReadAt = value ? DateTimeOffset.UtcNow : null; }, ct);
    public async Task SetArchivedAsync(Guid id, bool value, CancellationToken ct = default) => await UpdateAsync(id, n => { n.IsArchived = value; n.ArchivedAt = value ? DateTimeOffset.UtcNow : null; }, ct);
    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => await UpdateAsync(id, n => { n.IsDeleted = true; n.DeletedAt = DateTimeOffset.UtcNow; }, ct);
    public async Task SetSensitiveAsync(string packageName, bool value, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var app = await db.Apps.SingleAsync(x => x.PackageName == packageName, ct);
        app.IsSensitive = value;
        app.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (value) reveals.Clear();
    }

    public async Task<IReadOnlyList<FilterCount>> DeviceCountsAsync(IReadOnlyCollection<string>? packageNames = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Notifications.Where(x => !x.IsDeleted && !x.IsGroupSummary);
        if (packageNames is { Count: > 0 }) q = q.Where(x => packageNames.Contains(x.PackageName));
        return await q.GroupBy(x => x.DeviceId).Select(g => new FilterCount(g.Key, g.LongCount(x => !x.IsRead), g.LongCount())).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FilterCount>> AppCountsAsync(IReadOnlyCollection<string>? deviceIds = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Notifications.Where(x => !x.IsDeleted && !x.IsGroupSummary);
        if (deviceIds is { Count: > 0 }) q = q.Where(x => deviceIds.Contains(x.DeviceId));
        return await q.GroupBy(x => x.PackageName).Select(g => new FilterCount(g.Key, g.LongCount(x => !x.IsRead), g.LongCount())).ToListAsync(ct);
    }

    private async Task UpdateAsync(Guid id, Action<NotificationEntity> action, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var notification = await db.Notifications.FindAsync([id], ct) ?? throw new KeyNotFoundException();
        action(notification);
        notification.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RetentionService(IDbContextFactory<EasyNotyDbContext> dbFactory)
{
    public async Task<(int Archived, int Deleted)> ApplyAsync(RetentionPolicy policy, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var archived = 0;
        var deleted = 0;
        if (policy.ArchiveAfterDays is { } archiveDays)
        {
            var cutoff = now.AddDays(-archiveDays);
            archived = await db.Notifications.Where(x => x.PostedAt < cutoff && !x.IsArchived && !x.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsArchived, true).SetProperty(x => x.ArchivedAt, now).SetProperty(x => x.UpdatedAt, now), ct);
        }
        if (policy.DeleteAfterDays is { } deleteDays)
        {
            var cutoff = now.AddDays(-deleteDays);
            deleted = await db.Notifications.Where(x => x.PostedAt < cutoff && !x.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAt, now).SetProperty(x => x.UpdatedAt, now), ct);
        }
        return (archived, deleted);
    }
}
