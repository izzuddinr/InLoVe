using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Extensions.DependencyInjection;
using Qatalyst.Objects;
using CliWrap;
using CliWrap.Buffered;
using Newtonsoft.Json;

namespace Qatalyst.Services;

public class LogcatService
{
    private LogStorageService? _logStorageService;
    private PackageNameService? _packageNameService;
    private AdbProcessManager _processManager;

    public async Task StartLogcat(string device)
    {
        _logStorageService = App.Services.GetService<LogStorageService>();
        _packageNameService = App.Services.GetService<PackageNameService>();

        Console.WriteLine("Setting ADB Logcat buffer size to 32Mb.");
        await SetAdbLogcatBuffer(device);

        Console.WriteLine("Starting logcat process.");

        await Task.Run(async () =>
        {
            try
            {
                var command = Cli.Wrap("adb")
                    .WithArguments($"-s {device} logcat")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(async line =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;

                        var logEntry = line.CreateFromLogLine();

                        if (logEntry is not { IsValid: true }) return;

                        if (_packageNameService != null && logEntry.ProcessId != null)
                            logEntry.PackageName = _packageNameService.GetPackageName(logEntry.ProcessId);

                        if (string.IsNullOrEmpty(logEntry.PackageName)) return;

                        if (_logStorageService != null)
                            await _logStorageService.SaveLogEntryAsync(logEntry);
                    }));

                await command.ExecuteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading logcat output: {ex.Message}");
                Console.WriteLine($"Trace: {ex.StackTrace}");
            }
        });
    }

    public async Task StartLogcat(StorageFile file)
    {
        _logStorageService = App.Services.GetService<LogStorageService>();
        _packageNameService = App.Services.GetService<PackageNameService>();

        await Task.Run(async () =>
        {
            try
            {
                var logEntries = await ReadLogEntriesFromJsonAsync(file);

                // Filter entries where ProcessId and PackageName are not null or empty
                var validEntries = logEntries
                    .Where(entry => !string.IsNullOrEmpty(entry.ProcessId) && !string.IsNullOrEmpty(entry.PackageName));

                // Create a dictionary with ProcessId as the key and PackageName as the value
                var packageCache = validEntries
                    .GroupBy(entry => entry.ProcessId) // Group by ProcessId to ensure uniqueness
                    .ToDictionary(group => group.Key, group => group.First().PackageName);

                _packageNameService?.BuildPackageNameCacheFromFile(packageCache);

                foreach (var entry in logEntries.Where(entry => !string.IsNullOrEmpty(entry.PackageName)))
                {
                    if (_logStorageService != null)
                        await _logStorageService.SaveLogEntryAsync(entry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading logcat output: {ex.Message}");
                Console.WriteLine($"Trace: {ex.StackTrace}");
            }
        });
    }

    public static async Task<List<LogEntry>> ReadLogEntriesFromJsonAsync(StorageFile file)
    {
        var jsonContent = await FileIO.ReadTextAsync(file);
        return JsonConvert.DeserializeObject<List<LogEntry>>(jsonContent) ?? [];
    }

    public void StopLogcat()
    {
        Console.WriteLine("Stopping logcat process.");
        try
        {
            _processManager.KillAllManagedProcesses();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping logcat process: {ex.Message}");
        }
        Console.WriteLine("Logcat process stopped.");
    }

    private async Task SetAdbLogcatBuffer(string device)
    {
        Console.WriteLine($"Setting logcat buffer size for device {device}.");

        var result = await Cli.Wrap("adb")
            .WithArguments($"-s {device} logcat -G 32M")
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        Console.WriteLine(result.StandardOutput);
        Console.WriteLine("Logcat buffer size set to 32Mb.");
    }

    private async Task ClearLogcatBuffer(string device)
    {
        Console.WriteLine($"Clearing logcat buffer for device {device}.");

        var result = await Cli.Wrap("adb")
            .WithArguments($"-s {device} logcat -c")
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        Console.WriteLine(result.StandardOutput);
        Console.WriteLine("Logcat buffer cleared.");
    }
}
