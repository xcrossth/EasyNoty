using System.Diagnostics;
using System.Security.Principal;

const int DefaultPort = 38492;
Console.WriteLine("EasyNoty Windows setup helper");
Console.WriteLine("The desktop application itself always runs as a normal user.");

if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
{
    Console.Error.WriteLine("Windows 10 build 19041 or newer is required.");
    return 2;
}

var admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
if (!admin)
{
    Console.Error.WriteLine("Setup must be elevated to change the Private-network firewall rule.");
    return 3;
}

var port = DefaultPort;
if (args.Length >= 2 && args[0] == "--port" && (!int.TryParse(args[1], out port) || port is < 1 or > 65535))
{
    Console.Error.WriteLine("Port must be between 1 and 65535.");
    return 4;
}

var process = Process.Start(new ProcessStartInfo
{
    FileName = "netsh.exe",
    Arguments = $"advfirewall firewall add rule name=\"EasyNoty LAN Receiver\" dir=in action=allow protocol=TCP localport={port} profile=private",
    UseShellExecute = false
});
if (process is null) return 5;
await process.WaitForExitAsync();
if (process.ExitCode != 0)
{
    Console.Error.WriteLine($"Firewall configuration failed with exit code {process.ExitCode}.");
    return process.ExitCode;
}

Console.WriteLine($"Private-network TCP port {port} is allowed.");
Console.WriteLine("Install the Microsoft Windows App Runtime 2.2 from Microsoft's signed installer if EasyNoty reports that the framework is missing.");
return 0;
