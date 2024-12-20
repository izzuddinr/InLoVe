using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Qatalyst.Objects;

namespace Qatalyst.Services;

public class PackageNameService
{
    private static readonly List<string> _excludePackages =
    [
        "ueventd",
        "logd",
        "servicemanager",
        "thermal-engine",
        "storaged",
        "time_daemon",
        "surfaceflinger",
        "installd",
        "keystore",
        "wificond",
        "vold",

        // Drivers and Kernel Threads
        "kworker",
        "bioset",
        "irq",
        "rcu",
        "kdmflush",
        "mmcqd",
        "dm_bufio_cache",
        "ext4-rsv-conver",
        "mdss_fb0",

        // Logcat Process Management
        "ps",
        "adbd",

        // Networking Services
        "netd",
        "wpa_supplicant",
        "cnss_daemon",

        // Media and Framework Daemons
        "android.process.media",
        "android.system.suspend",
        "android.hardware.light",
        "media.metrics",
        "media.extractor",
        "mediaserver",
        "audioserver",

        // Low-Value Kernel Services
        "diag",
        "kgsl",
        "qmi",
        "usb_bam_wq",
        "sched",
        "sysmon",
        "ipv6_addrconf",

        // Miscellaneous
        "tombstoned",
        "timesync_server",
        "customconfigsd",
        "apexd",
        "ashmemd",
        "SENSORS",
        "MODEM",
        "DIAG",
        "WCNSS",
        "LPASS",
        "CDSP",
        "kthread"
    ];

    private readonly PubSubService _pubSubService;

    private Dictionary<string, string> _packageCache = new();

    public PackageNameService()
    {
        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService.Subscribe("DeviceSelected", OnDeviceSelected);
    }

    private async void OnDeviceSelected(object eventData)
    {
        try
        {
            if (eventData is not DeviceInfo selectedDevice) return;
            Console.WriteLine($"Device selected: {selectedDevice.SerialNumber}");
            await BuildPackageNameCacheAsync(selectedDevice);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error on Device selected event: {ex.Message}");
        }
    }

    public Task BuildPackageNameCacheFromFile(Dictionary<string, string> packageCache)
    {
        _packageCache = packageCache;
        _pubSubService.Publish("PackageCacheInitialized",
            new Tuple<List<string>, List<string>>(GetRunningPackages(), GetDefaultPackage()));

        return Task.CompletedTask;
    }

    private async Task BuildPackageNameCacheAsync(DeviceInfo? deviceInfo)
    {
        if (deviceInfo is null) return;

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = $"-s {deviceInfo.SerialNumber} shell ps",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var lines = output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        _packageCache.Clear();
        foreach (var line in lines)
        {
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length <= 8)
            {
                continue;
            }

            var rawPackageName = parts[8];

            if (_excludePackages.Any(exclude => rawPackageName.Contains(exclude))) continue;

            var packageName = SanitizePackageName(rawPackageName);

            _packageCache[parts[1]] = packageName;
            Console.WriteLine($"Processed packaged: {parts[1]} | {packageName}");
        }

        _pubSubService.Publish("PackageCacheInitialized",
            new Tuple<List<string>, List<string>>(GetRunningPackages(), GetDefaultPackage()));
    }

    public List<string> GetRunningPackages()
    {
        return _packageCache.Values.Distinct().ToList();
    }

    private List<string> GetDefaultPackage()
    {
        var filteredPackages = _packageCache
            .Where(package => package.Value.Contains(".ingenico") || package.Value.Contains(".ingp"))
            .Select(package => package.Value)
            .ToList();

        return filteredPackages;
    }

    public string GetPackageName(string processId)
    {
        return _packageCache.TryGetValue(processId, out var packageName) ? packageName : string.Empty;
    }

    private static string SanitizePackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return string.Empty;
        }

        packageName = packageName.Trim();

        foreach (var c in packageName.Where(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_'))
        {
            packageName = packageName.Replace(c, '_');
        }

        return packageName;
    }
}