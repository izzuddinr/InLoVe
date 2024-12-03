using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Qatalyst.Objects;

namespace Qatalyst.Services
{
    public class DeviceService
    {
        private readonly AdbProcessManager _processManager;
        public ObservableCollection<string> AvailableDevices { get; private set; } = new();

        public DeviceService(IServiceProvider serviceProvider)
        {
            _processManager = serviceProvider.GetService<AdbProcessManager>()
                ?? throw new InvalidOperationException("AdbProcessManager is not registered in the service provider.");
        }

        public async Task<List<DeviceInfo>> GetConnectedDevicesWithDetails()
        {
            var connectedDevices = await GetConnectedDevices();
            var detailedDevices = new List<DeviceInfo>();

            foreach (var deviceId in connectedDevices)
            {
                var deviceInfo = await GetDeviceDetailsAsync(deviceId);
                detailedDevices.Add(deviceInfo);
            }

            return detailedDevices;
        }

        public async Task<List<string>> GetConnectedDevices()
        {
            Console.WriteLine("Starting to fetch connected devices...");
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

            try
            {
                Console.WriteLine("Creating and managing adb process...");
                process.Start();
                AvailableDevices.Clear();

                Console.WriteLine("Reading adb output...");
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    if (!IsValidDeviceLine(line, out var deviceId)) continue;
                    AvailableDevices.Add(deviceId);
                    Console.WriteLine($"Device detected: {deviceId}");
                }

                await process.WaitForExitAsync();
                Console.WriteLine("Process exited.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while fetching devices: {ex.Message}");
            }
            finally
            {
                process.Dispose();
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
                string GetAdbOutput(string command) =>
                    RunAdbCommand($"-s {serialNumber} shell {command}").Trim();

                var manufacturer = GetAdbOutput("getprop ro.product.manufacturer");
                var model = GetAdbOutput("getprop ro.product.model");
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

        public string RunAdbCommand(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
    }
}
