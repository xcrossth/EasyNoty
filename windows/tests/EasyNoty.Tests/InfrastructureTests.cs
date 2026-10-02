using EasyNoty.Core;
using EasyNoty.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyNoty.Tests;

public sealed class InfrastructureTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "EasyNoty.Tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        var paths = new StoragePaths(_root, Path.Combine(_root, "test.db"), Path.Combine(_root, "assets"), Path.Combine(_root, "logs"), true, null);
        Directory.CreateDirectory(paths.Assets);
        Directory.CreateDirectory(paths.Logs);
        var services = new ServiceCollection().AddLogging().AddEasyNoty(paths, useDpapi: false);
        _provider = services.BuildServiceProvider();
        await _provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }

    [Fact]
    public async Task Duplicate_event_is_idempotent_and_ongoing_event_appends_revision()
    {
        var receiver = _provider.GetRequiredService<NotificationReceiver>();
        var first = CoreTests.TestEvent("app.chat", "{\"title\":\"A\",\"text\":\"one\"}", ongoing: true, revision: 0, instance: "ongoing-1");
        var accepted = await receiver.ReceiveAsync(first);
        var duplicate = await receiver.ReceiveAsync(first);
        var second = CoreTests.TestEvent("app.chat", "{\"title\":\"A\",\"text\":\"two\"}", ongoing: true, revision: 1, instance: "ongoing-1");
        await receiver.ReceiveAsync(second);
        Assert.False(accepted.Ack.Duplicate);
        Assert.True(duplicate.Ack.Duplicate);
        Assert.Equal(accepted.Ack.NotificationId, duplicate.Ack.NotificationId);
        await using var db = await DbFactory.CreateDbContextAsync();
        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Equal(2, await db.NotificationRevisions.CountAsync());
        Assert.Equal("two", (await db.Notifications.SingleAsync()).Body);
    }

    [Fact]
    public async Task Normal_posts_create_separate_logical_items_and_group_summary_is_hidden()
    {
        var receiver = _provider.GetRequiredService<NotificationReceiver>();
        await receiver.ReceiveAsync(CoreTests.TestEvent("app.chat", "{\"text\":\"one\"}", instance: "same"));
        await receiver.ReceiveAsync(CoreTests.TestEvent("app.chat", "{\"text\":\"two\"}", instance: "same"));
        await receiver.ReceiveAsync(CoreTests.TestEvent("app.chat", "{\"text\":\"summary\"}", summary: true));
        Assert.Equal(2, (await _provider.GetRequiredService<InboxService>().QueryAsync(new())).Count);
        await using var db = await DbFactory.CreateDbContextAsync();
        Assert.Equal(3, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Sensitive_content_requires_explicit_item_reveal()
    {
        var receiver = _provider.GetRequiredService<NotificationReceiver>();
        var accepted = await receiver.ReceiveAsync(CoreTests.TestEvent("app.bank", "{\"title\":\"Bank\",\"text\":\"Secret\"}"));
        var inbox = _provider.GetRequiredService<InboxService>();
        await inbox.SetSensitiveAsync("app.bank", true);
        var hidden = Assert.Single(await inbox.QueryAsync(new()));
        Assert.Equal("Content hidden", hidden.Title);
        _provider.GetRequiredService<SensitiveRevealSession>().ClearExcept(accepted.Ack.NotificationId);
        var shown = Assert.Single(await inbox.QueryAsync(new()));
        Assert.Equal("Secret", shown.Preview);
    }

    [Fact]
    public async Task Filters_use_device_and_app_intersection_and_soft_delete_hides_item()
    {
        var receiver = _provider.GetRequiredService<NotificationReceiver>();
        var accepted = await receiver.ReceiveAsync(CoreTests.TestEvent("app.one", "{\"text\":\"match\"}"));
        await receiver.ReceiveAsync(CoreTests.TestEvent("app.two", "{\"text\":\"other\"}"));
        var inbox = _provider.GetRequiredService<InboxService>();
        var rows = await inbox.QueryAsync(new(DeviceIds: ["phone-1"], PackageNames: ["app.one"], Search: "match"));
        Assert.Single(rows);
        await inbox.SoftDeleteAsync(accepted.Ack.NotificationId);
        Assert.Empty(await inbox.QueryAsync(new(PackageNames: ["app.one"])));
        await using var db = await DbFactory.CreateDbContextAsync();
        Assert.True((await db.Notifications.FindAsync(accepted.Ack.NotificationId))!.IsDeleted);
    }

    [Fact]
    public async Task Retention_uses_posted_time_and_never_hard_deletes()
    {
        var now = DateTimeOffset.UtcNow;
        await _provider.GetRequiredService<NotificationReceiver>().ReceiveAsync(CoreTests.TestEvent("app.old", "{\"text\":\"old\"}", posted: now.AddDays(-31)));
        var result = await _provider.GetRequiredService<RetentionService>().ApplyAsync(new(), now);
        Assert.Equal(1, result.Archived);
        Assert.Equal(0, result.Deleted);
        await using var db = await DbFactory.CreateDbContextAsync();
        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.True((await db.Notifications.SingleAsync()).IsArchived);
    }

    [Fact]
    public async Task Asset_store_verifies_hash_and_keeps_canonical_bytes()
    {
        var bytes = "asset-content"u8.ToArray();
        var hash = Hashing.Sha256Hex(bytes);
        var ack = await _provider.GetRequiredService<AssetStore>().StoreAsync(hash, "application/octet-stream", bytes);
        Assert.Equal(hash, ack.Hash);
        await Assert.ThrowsAsync<InvalidDataException>(() => _provider.GetRequiredService<AssetStore>().StoreAsync(new string('0', 64), "application/octet-stream", bytes));
        await using var db = await DbFactory.CreateDbContextAsync();
        var asset = await db.Assets.SingleAsync();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(asset.LocalPath));
    }

    private IDbContextFactory<EasyNotyDbContext> DbFactory => _provider.GetRequiredService<IDbContextFactory<EasyNotyDbContext>>();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }
}
