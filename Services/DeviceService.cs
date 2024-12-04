using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.DependencyInjection;
using Qatalyst.Objects;

namespace Qatalyst.Services;

public class DeviceService
{
    public ObservableCollection<string> AvailableDevices { get; private set; } = [];

    public DeviceInfo SelectedDevice { get; private set; }

    public DeviceService()
    {
        SelectedDevice = new DeviceInfo();
    }

    public async Task<List<DeviceInfo>> GetConnectedDevicesWithDetails()
    {
        var connectedDevices = await GetConnectedDevices();
        var detailedDevices = new List<DeviceInfo>();

        foreach (var deviceId in connectedDevices)
        {
            var deviceInfo = await GetDeviceDetailsAsync(deviceId);
            if (deviceInfo != null)
            {
                detailedDevices.Add(deviceInfo);
            }
        }

        return detailedDevices;
    }

    public async Task<List<string>> GetConnectedDevices()
    {
        Console.WriteLine("Starting to fetch connected devices...");
        AvailableDevices.Clear();

        try
        {
            Console.WriteLine("Running adb devices command...");
            var result = await Cli.Wrap("adb")
                .WithArguments("devices")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            Console.WriteLine("Processing adb output...");
            var lines = result.StandardOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (!IsValidDeviceLine(line, out var deviceId)) continue;
                AvailableDevices.Add(deviceId);
                Console.WriteLine($"Device detected: {deviceId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred while fetching devices: {ex.Message}");
        }

        return AvailableDevices.ToList();
    }

    private static bool IsValidDeviceLine(string line, out string deviceId)
    {
        deviceId = string.Empty;

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("List of devices")) return false;

        var parts = line.Split('\t');
        if (parts.Length > 1 && parts[1].Trim() == "device")
        {
            deviceId = parts[0];
            return true;
        }

        return false;
    }

    public async Task<DeviceInfo> GetDeviceDetailsAsync(string serialNumber)
    {
        try
        {
            async Task<string> GetAdbOutputAsync(string command)
            {
                var result = await Cli.Wrap("adb")
                    .WithArguments($"-s {serialNumber} shell {command}")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                return result.StandardOutput.Trim();
            }

            var manufacturer = await GetAdbOutputAsync("getprop ro.product.manufacturer");
            var model = await GetAdbOutputAsync("getprop ro.product.model");
            var imageLocationInput = (manufacturer, model);

            var deviceInfo = new DeviceInfo
            {
                SerialNumber = serialNumber,
                Manufacturer = manufacturer,
                Model = model,
                ImageLocation = imageLocationInput.GetImageLocation()
            };

            Console.WriteLine($"Device info: {deviceInfo}");

            return deviceInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching details for device {serialNumber}: {ex.Message}");
            return null;
        }
    }

    public async Task<string> RunAdbCommand(string arguments)
    {
        try
        {
            Console.WriteLine($"Running adb command: adb {arguments}");
            var result = await Cli.Wrap("adb")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            Console.WriteLine($"Adb response: {result.StandardOutput}");

            return result.StandardOutput.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running adb command '{arguments}': {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<string> RunAdbChainedCommands(IEnumerable<string> commands)
    {
        try
        {
            var combinedOutput = new StringBuilder();

            foreach (var arguments in commands)
            {
                Console.WriteLine($"Running adb command: adb {arguments}");

                var result = await Cli.Wrap("adb")
                    .WithArguments(arguments)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                Console.WriteLine($"Adb response for '{arguments}': {result.StandardOutput}");

                combinedOutput.AppendLine(result.StandardOutput.Trim());
            }

            return combinedOutput.ToString().Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running chained adb commands: {ex.Message}");
            return string.Empty;
        }
    }
}