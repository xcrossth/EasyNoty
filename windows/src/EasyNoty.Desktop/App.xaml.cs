using EasyNoty.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EasyNoty.Desktop;

public partial class App : Application
{
    private static readonly Mutex InstanceMutex;
    private static readonly bool IsFirstInstance;
    private IHost? _host;
    private MainWindow? _window;
    private bool _allowClose;

    static App() => InstanceMutex = new(true, "Local\\EasyNoty.Desktop.SingleInstance", out IsFirstInstance);
    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!IsFirstInstance) { Exit(); return; }
        try
        {
            var paths = StoragePathResolver.Resolve(AppContext.BaseDirectory);
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddEasyNoty(paths);
            builder.Services.AddSingleton<IToastNotifier, WindowsToastNotifier>();
            builder.Services.AddEasyNotyReceiver();
            _host = builder.Build();
            await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            await _host.StartAsync();
            _window = new MainWindow(_host.Services, paths);
            _window.TrayIcon.ExitRequested += async (_, _) =>
            {
                _allowClose = true;
                _window.Close();
            };
            _window.AppWindow.Closing += (_, e) =>
            {
                if (_allowClose) return;
                e.Cancel = true;
                _window.HideToBackground();
            };
            _window.Closed += async (_, _) =>
            {
                _window.TrayIcon.Dispose();
                if (_host is not null) await _host.StopAsync(TimeSpan.FromSeconds(5));
                WindowsToastNotifier.Unregister();
                InstanceMutex.ReleaseMutex();
            };
            WindowsToastNotifier.NotificationInvoked += HandleNotificationInvoked;
            _window.Activate();
            if (WindowsToastNotifier.TryGetNotificationId(args.Arguments, out var notificationId))
                _window.DispatcherQueue.TryEnqueue(async () => await _window.FocusNotificationAsync(notificationId));
        }
        catch (Exception ex)
        {
            var diagnosticRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyNoty");
            Directory.CreateDirectory(diagnosticRoot);
            await File.WriteAllTextAsync(Path.Combine(diagnosticRoot, "startup-error.log"), ex.ToString());
            var dialog = new ContentDialog { Title = "EasyNoty could not start", Content = ex.Message, CloseButtonText = "Exit", XamlRoot = _window?.Content.XamlRoot };
            if (_window is not null) await dialog.ShowAsync();
            Exit();
        }
    }

    private void HandleNotificationInvoked(Guid notificationId)
    {
        if (_window is null) return;
        _window.DispatcherQueue.TryEnqueue(async () => await _window.FocusNotificationAsync(notificationId));
    }
}
