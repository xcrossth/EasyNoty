using EasyNoty.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EasyNoty.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddEasyNoty(this IServiceCollection services, StoragePaths paths, ReceiverOptions? options = null, bool useDpapi = true)
    {
        services.AddPooledDbContextFactory<EasyNotyDbContext>(builder => builder
            .UseSqlite($"Data Source={paths.Database};Pooling=True")
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        services.AddSingleton(paths);
        services.AddSingleton(options ?? new ReceiverOptions(PcDeviceId: LoadOrCreatePcId(paths)));
        services.AddSingleton<SensitiveRevealSession>();
        services.AddSingleton<CertificateService>();
        services.AddSingleton<ISecretProtector>(_ => useDpapi ? new DpapiSecretProtector() : new TestSecretProtector());
        services.AddSingleton<INotificationParser, GenericAndroidParser>();
        services.AddSingleton<INotificationParser, GmailFallbackParser>();
        services.AddSingleton<INotificationParser, MessagingStyleParser>();
        services.AddSingleton<NotificationParserEngine>();
        services.AddSingleton<IToastNotifier, NullToastNotifier>();
        services.AddSingleton<NotificationReceiver>();
        services.AddSingleton<InboxService>();
        services.AddSingleton<RetentionService>();
        services.AddSingleton<PairingService>();
        services.AddSingleton<AssetStore>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<DeviceService>();
        services.AddSingleton<ReparseService>();
        services.AddSingleton<StorageMigrationService>();
        return services;
    }

    public static IServiceCollection AddEasyNotyReceiver(this IServiceCollection services)
    {
        services.AddHostedService<ReceiverWebSocketService>();
        services.AddHostedService<MdnsAdvertisementService>();
        services.AddHostedService<RetentionBackgroundService>();
        return services;
    }

    private static string LoadOrCreatePcId(StoragePaths paths)
    {
        var file = Path.Combine(paths.Root, "pc-device-id");
        if (File.Exists(file)) return File.ReadAllText(file).Trim();
        var id = Guid.NewGuid().ToString("D").ToLowerInvariant();
        File.WriteAllText(file, id);
        return id;
    }
}
