using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyNoty.Infrastructure;

/// <summary>
/// Advertises the receiver as <c>_easynoty._tcp.local</c> using the Windows 10 DNS-SD API.
/// QR pairing remains available if a network blocks multicast DNS.
/// </summary>
public sealed class MdnsAdvertisementService : IHostedService
{
    private const uint DnsQueryRequestVersion1 = 1;
    private const uint DnsRequestPending = 9506;
    private readonly ReceiverOptions _options;
    private readonly ILogger<MdnsAdvertisementService> _logger;
    private readonly DnsServiceRegisterComplete _completion;
    private IntPtr _instance;
    private IntPtr _request;

    public MdnsAdvertisementService(ReceiverOptions options, ILogger<MdnsAdvertisementService> logger)
    {
        _options = options;
        _logger = logger;
        _completion = OnCompleted;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var instanceName = $"EasyNoty-{_options.PcDeviceId[..Math.Min(8, _options.PcDeviceId.Length)]}._easynoty._tcp.local";
            var hostName = Dns.GetHostName() + ".local";
            _instance = DnsServiceConstructInstance(instanceName, hostName, IntPtr.Zero, IntPtr.Zero, checked((ushort)_options.Port), 0, 0, 0, IntPtr.Zero, IntPtr.Zero);
            if (_instance == IntPtr.Zero) throw new InvalidOperationException($"DnsServiceConstructInstance failed ({Marshal.GetLastWin32Error()}).");

            var request = new DnsServiceRegisterRequest
            {
                Version = DnsQueryRequestVersion1,
                InterfaceIndex = 0,
                ServiceInstance = _instance,
                Completion = _completion,
                QueryContext = IntPtr.Zero,
                Credentials = IntPtr.Zero,
                UnicastEnabled = 0,
            };
            _request = Marshal.AllocHGlobal(Marshal.SizeOf<DnsServiceRegisterRequest>());
            Marshal.StructureToPtr(request, _request, false);
            var result = DnsServiceRegister(_request, IntPtr.Zero);
            if (result != DnsRequestPending) throw new InvalidOperationException($"DnsServiceRegister failed ({result}).");
            _logger.LogInformation("EasyNoty DNS-SD advertisement started: {InstanceName}", instanceName);
        }
        catch (Exception ex)
        {
            // Discovery is convenience only; pairing QR contains complete endpoint data.
            _logger.LogWarning(ex, "EasyNoty DNS-SD advertisement is unavailable; QR pairing remains available.");
            ReleaseUnregisteredResources();
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_request != IntPtr.Zero)
        {
            var result = DnsServiceDeRegister(_request, IntPtr.Zero);
            if (result != DnsRequestPending) _logger.LogDebug("DNS-SD deregistration returned {Result}", result);
            // Both request and instance are retained until process exit because the DNS API completes asynchronously.
        }
        return Task.CompletedTask;
    }

    private void OnCompleted(uint status, IntPtr queryContext, IntPtr serviceInstance)
    {
        if (status != 0) _logger.LogWarning("DNS-SD advertisement operation completed with status {Status}", status);
    }

    private void ReleaseUnregisteredResources()
    {
        if (_request != IntPtr.Zero) { Marshal.FreeHGlobal(_request); _request = IntPtr.Zero; }
        if (_instance != IntPtr.Zero) { DnsServiceFreeInstance(_instance); _instance = IntPtr.Zero; }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void DnsServiceRegisterComplete(uint status, IntPtr queryContext, IntPtr serviceInstance);

    [StructLayout(LayoutKind.Sequential)]
    private struct DnsServiceRegisterRequest
    {
        public uint Version;
        public uint InterfaceIndex;
        public IntPtr ServiceInstance;
        public DnsServiceRegisterComplete Completion;
        public IntPtr QueryContext;
        public IntPtr Credentials;
        public int UnicastEnabled;
    }

    [DllImport("dnsapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr DnsServiceConstructInstance(string serviceName, string hostName, IntPtr ip4, IntPtr ip6, ushort port, ushort priority, ushort weight, uint propertyCount, IntPtr keys, IntPtr values);

    [DllImport("dnsapi.dll", SetLastError = true)]
    private static extern uint DnsServiceRegister(IntPtr request, IntPtr cancel);

    [DllImport("dnsapi.dll", SetLastError = true)]
    private static extern uint DnsServiceDeRegister(IntPtr request, IntPtr cancel);

    [DllImport("dnsapi.dll")]
    private static extern void DnsServiceFreeInstance(IntPtr instance);
}
