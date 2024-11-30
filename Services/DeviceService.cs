using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Qatalyst.Services;

public class DeviceService
{
    private ObservableCollection<string> AvailableDevices = [];

    public async Task<List<string>> GetConnectedDevices()
    {
        var _processManager = App.Services.GetService<AdbProcessManager>();

        Console.WriteLine("Creating process...");
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
        Console.WriteLine("process created...");
        Console.WriteLine("process added to manager...");
        process.Start();
        _processManager.AddToManagedProcess(process);
        Console.WriteLine("process started...");
        AvailableDevices.Clear();
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