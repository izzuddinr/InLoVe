using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace InLoVe.Services;

public class DeviceService
{
    public async Task<List<string>> GetConnectedDevices()
    {
        var deviceList = new List<string>();

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
            deviceList.Add(deviceId);
        }

        await process.WaitForExitAsync();

        return deviceList;
    }
}