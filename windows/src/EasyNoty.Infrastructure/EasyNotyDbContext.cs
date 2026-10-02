using EasyNoty.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EasyNoty.Infrastructure;

public sealed class EasyNotyDbContext(DbContextOptions<EasyNotyDbContext> options) : DbContext(options)
{
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<AppEntity> Apps => Set<AppEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationRevisionEntity> NotificationRevisions => Set<NotificationRevisionEntity>();
    public DbSet<NotificationParsedResultEntity> NotificationParsedResults => Set<NotificationParsedResultEntity>();
    public DbSet<AssetEntity> Assets => Set<AssetEntity>();
    public DbSet<NotificationAssetEntity> NotificationAssets => Set<NotificationAssetEntity>();
    public DbSet<PairingSessionEntity> PairingSessions => Set<PairingSessionEntity>();
    public DbSet<ProcessedMessageEntity> ProcessedMessages => Set<ProcessedMessageEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DeviceEntity>(x => { x.HasKey(v => v.Id); x.HasIndex(v => v.DeviceId).IsUnique(); });
        model.Entity<AppEntity>(x => { x.HasKey(v => v.Id); x.HasIndex(v => v.PackageName).IsUnique(); });
        model.Entity<NotificationEntity>(x =>
        {
            x.HasKey(v => v.Id);
            x.HasIndex(v => new { v.DeviceId, v.NotificationInstanceId });
            x.HasIndex(v => new { v.PostedAt, v.IsDeleted, v.IsArchived });
        });
        model.Entity<NotificationRevisionEntity>(x =>
        {
            x.HasKey(v => v.Id);
            x.HasIndex(v => new { v.DeviceId, v.EventId }).IsUnique();
            x.HasIndex(v => new { v.NotificationId, v.RevisionIndex }).IsUnique();
            x.HasOne(v => v.Notification).WithMany(v => v.Revisions).HasForeignKey(v => v.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<NotificationParsedResultEntity>(x =>
        {
            x.HasKey(v => v.Id);
            x.HasIndex(v => new { v.NotificationId, v.IsCurrent });
            x.HasOne(v => v.Notification).WithMany(v => v.ParsedResults).HasForeignKey(v => v.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AssetEntity>(x => x.HasKey(v => v.Hash));
        model.Entity<NotificationAssetEntity>(x =>
        {
            x.HasKey(v => v.Id);
            x.HasIndex(v => new { v.NotificationId, v.AssetHash, v.Role }).IsUnique();
            x.HasOne(v => v.Notification).WithMany(v => v.AssetMappings).HasForeignKey(v => v.NotificationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PairingSessionEntity>(x => { x.HasKey(v => v.Id); x.HasIndex(v => v.TokenHash).IsUnique(); });
        model.Entity<ProcessedMessageEntity>(x => x.HasKey(v => new { v.DeviceId, v.SessionId, v.MessageId }));
        model.Entity<SettingEntity>(x => x.HasKey(v => v.Key));

        var offsetConverter = new ValueConverter<DateTimeOffset, long>(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
        var nullableOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(value => value.HasValue ? value.Value.UtcTicks : null, value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
        foreach (var entity in model.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.ClrType == typeof(DateTimeOffset)) property.SetValueConverter(offsetConverter);
            else if (property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(nullableOffsetConverter);
        }
    }
}
