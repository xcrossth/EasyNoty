using System.Runtime.InteropServices;
using System.Security;
using EasyNoty.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;

namespace EasyNoty.Desktop;

public sealed class WindowsToastNotifier : IToastNotifier
{
    private static bool _registered;
    private static bool _sessionLocked;

    public static event Action? SessionLocked;
    public static event Action<Guid>? NotificationInvoked;

    public WindowsToastNotifier()
    {
        if (_registered) return;
        AppNotificationManager.Default.Register();
        AppNotificationManager.Default.NotificationInvoked += (_, args) =>
        {
            if (TryGetNotificationId(args.Arguments, out var notificationId)) NotificationInvoked?.Invoke(notificationId);
        };
        SystemEvents.SessionSwitch += (_, args) =>
        {
            if (args.Reason == SessionSwitchReason.SessionLock)
            {
                _sessionLocked = true;
                SessionLocked?.Invoke();
            }
            else if (args.Reason == SessionSwitchReason.SessionUnlock)
            {
                _sessionLocked = false;
            }
        };
        _registered = true;
    }

    public Task ShowAsync(Guid notificationId, string appName, string? title, string? body, bool redacted, CancellationToken ct = default)
    {
        redacted |= _sessionLocked;
        var safeApp = SecurityElement.Escape(appName);
        var safeTitle = SecurityElement.Escape(redacted ? "Content hidden" : title ?? appName);
        var safeBody = SecurityElement.Escape(redacted ? "Open EasyNoty to view this notification." : body ?? string.Empty);
        var xml = $"<toast launch=\"notification_id={notificationId:D}\"><visual><binding template=\"ToastGeneric\"><text>{safeApp}</text><text>{safeTitle}</text><text>{safeBody}</text></binding></visual></toast>";
        AppNotificationManager.Default.Show(new AppNotification(xml) { Tag = notificationId.ToString("N"), Group = "EasyNoty" });
        return Task.CompletedTask;
    }

    public static void Unregister()
    {
        if (_registered) AppNotificationManager.Default.Unregister();
        _registered = false;
    }

    public static bool TryGetNotificationId(string? arguments, out Guid notificationId)
    {
        notificationId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(arguments)) return false;

        foreach (var part in arguments.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tokens = part.Split('=', 2);
            if (tokens.Length != 2 || !tokens[0].Equals("notification_id", StringComparison.OrdinalIgnoreCase)) continue;
            return Guid.TryParse(Uri.UnescapeDataString(tokens[1]), out notificationId);
        }

        return false;
    }

    public static bool TryGetNotificationId(IDictionary<string, string>? arguments, out Guid notificationId)
    {
        notificationId = Guid.Empty;
        return arguments is not null
               && arguments.TryGetValue("notification_id", out var value)
               && Guid.TryParse(value, out notificationId);
    }
}

public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "EasyNoty";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(Name) is not null;
    }

    public static void SetEnabled(bool enabled, string executable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled) key.SetValue(Name, $"\"{executable}\"");
        else key.DeleteValue(Name, false);
    }
}

public sealed class TrayIconAdapter : IDisposable
{
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const int GwlWndproc = -4;
    private const int OpenCommandId = 1001;
    private const int ExitCommandId = 1002;
    private const int SwRestore = 9;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const int SmCxSmallIcon = 49;
    private const int SmCySmallIcon = 50;
    private static readonly Dictionary<nint, TrayIconAdapter> Instances = [];

    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private readonly WndProc _wndProc;
    private IntPtr _previousWndProc;
    private IntPtr _iconHandle;
    private readonly bool _ownsIconHandle;
    private bool _installed;

    public TrayIconAdapter(Window window, string iconPath)
    {
        _window = window;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _wndProc = WndProcRouter;
        if (File.Exists(iconPath))
        {
            _iconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, GetSystemMetrics(SmCxSmallIcon), GetSystemMetrics(SmCySmallIcon), LrLoadFromFile);
            _ownsIconHandle = _iconHandle != IntPtr.Zero;
        }
    }

    public event Action? OpenRequested;
    public event EventHandler? ExitRequested;

    public bool TryInstall()
    {
        try
        {
            _previousWndProc = SetWindowLongPtr(_hwnd, GwlWndproc, Marshal.GetFunctionPointerForDelegate(_wndProc));
            Instances[_hwnd] = this;
            var data = CreateNotifyIconData();
            _installed = Shell_NotifyIcon(NimAdd, ref data);
            return _installed;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_installed)
        {
            var data = CreateNotifyIconData();
            Shell_NotifyIcon(NimDelete, ref data);
            _installed = false;
        }

        if (_previousWndProc != IntPtr.Zero) SetWindowLongPtr(_hwnd, GwlWndproc, _previousWndProc);
        Instances.Remove(_hwnd);
        if (_ownsIconHandle && _iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }

    public static void BringToForeground(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        ShowWindow(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
    }

    private NotifyIconData CreateNotifyIconData() =>
        new()
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = 0x8001,
            hIcon = _iconHandle != IntPtr.Zero ? _iconHandle : LoadIcon(IntPtr.Zero, new IntPtr(32512)),
            szTip = "EasyNoty - receiver running"
        };

    private IntPtr HandleWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == 0x8001)
        {
            switch ((uint)lParam)
            {
                case 0x0202:
                    OpenRequested?.Invoke();
                    return IntPtr.Zero;
                case 0x0205:
                    ShowContextMenu();
                    return IntPtr.Zero;
            }
        }

        return CallWindowProc(_previousWndProc, hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        const uint mfString = 0x00000000;
        const uint tpmLeftAlign = 0x0000;
        const uint tpmBottomAlign = 0x0020;
        const uint tpmRightButton = 0x0002;
        const uint tpmReturnCmd = 0x0100;

        var menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, mfString, OpenCommandId, "Open EasyNoty");
            AppendMenu(menu, mfString, ExitCommandId, "Exit");
            GetCursorPos(out var point);
            SetForegroundWindow(_hwnd);
            var command = TrackPopupMenu(menu, tpmLeftAlign | tpmBottomAlign | tpmRightButton | tpmReturnCmd, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
            if (command == OpenCommandId) OpenRequested?.Invoke();
            if (command == ExitCommandId) ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr WndProcRouter(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam) =>
        Instances.TryGetValue(hwnd, out var instance)
            ? instance.HandleWndProc(hwnd, msg, wParam, lParam)
            : DefWindowProc(hwnd, msg, wParam, lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersion;
        public uint dwInfoFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int cx, int cy, uint loadFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CallWindowProc(IntPtr prevProc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, int id, string text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
