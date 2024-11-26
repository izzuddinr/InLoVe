using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Qatalyst.Services;

public class DeviceService
{
    public ObservableCollection<string> AvailableDevices = new();
    public string SelectedDevice { get; set; }

    public async Task<List<string>> GetConnectedDevices()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "devices",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("List of devices")) continue;

            var deviceId = line.Split('\t')[0];
            AvailableDevices.Add(deviceId);
        }

        await process.WaitForExitAsync();

        return AvailableDevices.ToList();
    }
}