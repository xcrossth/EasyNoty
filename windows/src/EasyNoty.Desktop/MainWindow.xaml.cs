using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using EasyNoty.Core;
using EasyNoty.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using QRCoder;

namespace EasyNoty.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly ObservableCollection<InboxRow> _rows = [];
    private readonly TrayIconAdapter _trayIcon;
    private readonly Microsoft.UI.Windowing.AppWindowTitleBar _titleBar;
    private readonly string _windowPlacementFile;
    private InboxItem? _selected;

    public MainWindow(IServiceProvider services, StoragePaths paths)
    {
        InitializeComponent();
        _services = services;
        _windowPlacementFile = Path.Combine(paths.Root, "window-placement.json");
        InboxList.ItemsSource = _rows;
        StorageText.Text = $"Storage: {paths.Root}" + (paths.IsPortable ? " (portable)" : $" (LocalAppData fallback: {paths.FallbackReason})");
        StartupToggle.IsOn = StartupRegistration.IsEnabled();
        ExtendsContentIntoTitleBar = true;
        _titleBar = AppWindow.TitleBar;
        UpdateTitleBarInsets();
        RestoreWindowPlacement();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "EasyNoty.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        Activated += async (_, args) =>
        {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            _services.GetRequiredService<SensitiveRevealSession>().Clear();
            SaveWindowPlacement();
        }
            await RefreshAsync();
        };
        WindowsToastNotifier.SessionLocked += ClearReveals;
        Closed += (_, _) =>
        {
            WindowsToastNotifier.SessionLocked -= ClearReveals;
        };
        _trayIcon = new TrayIconAdapter(this, iconPath);
        _trayIcon.OpenRequested += ShowFromBackground;
        _ = _trayIcon.TryInstall();
    }

    private void ClearReveals() => _services.GetRequiredService<SensitiveRevealSession>().Clear();
    private void UpdateTitleBarInsets()
    {
        var scale = Content?.XamlRoot?.RasterizationScale ?? 1d;
        // The title bar inset is expressed in physical pixels; XAML margins use effective pixels.
        // Keep a conservative minimum for startup, before WinUI reports the final inset.
        TopCommandBar.Margin = new Thickness(0, 0, Math.Max(180d, _titleBar.RightInset / scale), 0);
    }
    public TrayIconAdapter TrayIcon => _trayIcon;

    private async Task RefreshAsync()
    {
        var view = ViewSelector.SelectedIndex switch { 1 => InboxView.Archived, 2 => InboxView.All, _ => InboxView.Inbox };
        var devices = DeviceFilters.SelectedItems.Cast<FilterRow>().Select(x => x.Key).ToArray();
        var apps = AppFilters.SelectedItems.Cast<FilterRow>().Select(x => x.Key).ToArray();
        var data = await _services.GetRequiredService<InboxService>().QueryAsync(new(view, devices, apps, SearchBox.Text));
        _rows.Clear();
        foreach (var item in data) _rows.Add(new(item, $"{item.AppName} — {item.Title}", item.Preview ?? string.Empty, $"{item.DeviceId} · {item.PostedAt.LocalDateTime:g}" + (item.IsOversized ? " · Oversized raw" : string.Empty)));
        await RefreshFiltersAsync();
        StatusBar.Message = $"{data.Count} notification(s) · WSS port {_services.GetRequiredService<ReceiverOptions>().Port}";
    }

    private async Task RefreshFiltersAsync()
    {
        if (DeviceFilters.Items.Count == 0)
            foreach (var count in await _services.GetRequiredService<InboxService>().DeviceCountsAsync()) DeviceFilters.Items.Add(new FilterRow(count.Key, $"{count.Key} ({count.UnreadCount})"));
        if (AppFilters.Items.Count == 0)
            foreach (var count in await _services.GetRequiredService<InboxService>().AppCountsAsync()) AppFilters.Items.Add(new FilterRow(count.Key, $"{count.Key} ({count.UnreadCount})"));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void ViewSelector_Changed(object sender, SelectionChangedEventArgs e) { if (Content?.XamlRoot is not null) await RefreshAsync(); }
    private async void Filters_Changed(object sender, SelectionChangedEventArgs e) { if (Content?.XamlRoot is not null) await RefreshAsync(); }
    private async void Search_Submitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => await RefreshAsync();

    public async Task FocusNotificationAsync(Guid notificationId)
    {
        ShowFromBackground();
        SearchBox.Text = string.Empty;
        DeviceFilters.SelectedItems.Clear();
        AppFilters.SelectedItems.Clear();
        ViewSelector.SelectedIndex = 2;
        await RefreshAsync();
        var row = _rows.FirstOrDefault(x => x.Item.Id == notificationId);
        if (row is null) return;
        InboxList.SelectedItem = row;
        InboxList.ScrollIntoView(row);
    }

    public void ShowFromBackground()
    {
        AppWindow.Show();
        Activate();
        TrayIconAdapter.BringToForeground(this);
    }

    public void HideToBackground()
    {
        _services.GetRequiredService<SensitiveRevealSession>().Clear();
        SaveWindowPlacement();
        AppWindow.Hide();
    }

    private void RestoreWindowPlacement()
    {
        try
        {
            if (!File.Exists(_windowPlacementFile)) return;
            var saved = JsonSerializer.Deserialize<WindowPlacementState>(File.ReadAllText(_windowPlacementFile));
            if (saved is null || saved.Width < 480 || saved.Height < 320) return;
            var rect = new NativeRect { Left = saved.Left, Top = saved.Top, Right = saved.Left + saved.Width, Bottom = saved.Top + saved.Height };
            if (MonitorFromRect(ref rect, 0) == IntPtr.Zero) return;
            var placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>(), ShowCommand = saved.IsMaximized ? 3u : 1u, NormalPosition = rect };
            SetWindowPlacement(WinRT.Interop.WindowNative.GetWindowHandle(this), ref placement);
        }
        catch (Exception) { /* A bad or stale placement must never prevent startup. */ }
    }

    private void SaveWindowPlacement()
    {
        try
        {
            var placement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
            if (!GetWindowPlacement(WinRT.Interop.WindowNative.GetWindowHandle(this), ref placement)) return;
            var normal = placement.NormalPosition;
            var state = new WindowPlacementState(normal.Left, normal.Top, normal.Right - normal.Left, normal.Bottom - normal.Top, placement.ShowCommand == 3);
            var temporary = _windowPlacementFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, _windowPlacementFile, true);
        }
        catch (Exception) { /* Placement persistence is best effort. */ }
    }

    private async void InboxList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _services.GetRequiredService<SensitiveRevealSession>().Clear();
        _selected = (InboxList.SelectedItem as InboxRow)?.Item;
        DetailTitle.Text = _selected?.Title ?? "Select a notification";
        DetailBody.Text = _selected?.Preview ?? string.Empty;
        if (_selected is { IsRead: false })
        {
            await _services.GetRequiredService<InboxService>().SetReadAsync(_selected.Id, true);
            _selected = _selected with { IsRead = true };
        }
    }

    private async void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || !_selected.IsSensitive) return;
        var reveals = _services.GetRequiredService<SensitiveRevealSession>();
        reveals.Clear();
        reveals.Reveal(_selected.Id);
        await RefreshAsync();
    }
    private async void Read_Click(object sender, RoutedEventArgs e) { if (_selected is not null) { await _services.GetRequiredService<InboxService>().SetReadAsync(_selected.Id, true); await RefreshAsync(); } }
    private async void Archive_Click(object sender, RoutedEventArgs e) { if (_selected is not null) { await _services.GetRequiredService<InboxService>().SetArchivedAsync(_selected.Id, true); await RefreshAsync(); } }
    private async void Delete_Click(object sender, RoutedEventArgs e) { if (_selected is not null) { await _services.GetRequiredService<InboxService>().SoftDeleteAsync(_selected.Id); await RefreshAsync(); } }

    private async void Pair_Click(object sender, RoutedEventArgs e)
    {
        var options = _services.GetRequiredService<ReceiverOptions>();
        var cert = _services.GetRequiredService<CertificateService>().GetOrCreate();
        var endpoints = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses).Select(x => x.Address).Where(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(x)).Select(x => new PairingEndpoint(x.ToString(), options.Port)).Distinct().Take(8).ToArray();
        var pairing = _services.GetRequiredService<PairingService>();
        var ticket = await pairing.CreateAsync(options.PcDeviceId, options.PcName, CertificateService.Fingerprint(cert), endpoints);
        var qrJson = JsonSerializer.Serialize(ticket, ProtocolV1.Json);
        using var qrData = QRCodeGenerator.GenerateQrCode(qrJson, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(qrData).GetGraphic(8);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(png); await writer.StoreAsync(); writer.DetachStream(); }
        stream.Seek(0);
        var bitmap = new BitmapImage();
        bitmap.SetSource(stream);
        var status = new TextBlock { Text = "Scan this QR with EasyNoty Android, compare the six-digit verification code, then accept the pending device.", TextWrapping = TextWrapping.Wrap, MaxWidth = 380 };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new Image { Source = bitmap, Width = 320, Height = 320 });
        content.Children.Add(status);
        var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "Pair Android device", Content = content, PrimaryButtonText = "Accept pending device", CloseButtonText = "Cancel" };
        dialog.PrimaryButtonClick += (_, eventArgs) =>
        {
            var pending = pairing.PendingDeviceIds.FirstOrDefault();
            if (pending is null)
            {
                eventArgs.Cancel = true;
                status.Text = "No device is waiting yet. Scan the QR first, compare the code, then click Accept pending device again.";
                return;
            }
            pairing.Approve(pending);
        };
        await dialog.ShowAsync();
    }

    private void StartupToggle_Toggled(object sender, RoutedEventArgs e) => StartupRegistration.SetEnabled(StartupToggle.IsOn, Environment.ProcessPath!);

    private sealed record FilterRow(string Key, string Label) { public override string ToString() => Label; }
    private sealed record InboxRow(InboxItem Item, string Header, string Preview, string Metadata);
    private sealed record WindowPlacementState(int Left, int Top, int Width, int Height, bool IsMaximized);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public uint ShowCommand;
        public NativePoint MinPosition;
        public NativePoint MaxPosition;
        public NativeRect NormalPosition;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hwnd, [In] ref WindowPlacement placement);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);
}
