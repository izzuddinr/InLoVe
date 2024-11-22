using System;
using System.Diagnostics;
using System.Threading.Tasks;
using InLoVe.Objects;
using Microsoft.Extensions.DependencyInjection;

namespace InLoVe.Services;

public class LogcatService
{
    private readonly LogStorageService? _logStorageService;
    private readonly PackageNameService? _packageNameService;

    private Process? _logcatProcess;

    public LogcatService()
    {
        _logStorageService = App.Services.GetService<LogStorageService>();
        _packageNameService = App.Services.GetService<PackageNameService>();
    }

    public async Task StartLogcat(string device)
    {
        Console.WriteLine("Setting ADB Logcat buffer size to 32Mb.");
        await SetAdbLogcatBuffer(device);

        Console.WriteLine("Starting logcat process.");
        _logcatProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = $"-s {device} logcat",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _logcatProcess.Start();
        Console.WriteLine("Logcat process started.");

        await Task.Run(async () =>
        {
            try
            {
                using var reader = _logcatProcess.StandardOutput;
                while (await reader.ReadLineAsync() is { } line)
                {
                    var logEntry = LogEntry.CreateFromLogLine(line);

                    if (logEntry != null && logEntry.IsValid)
                    {
                        if (_packageNameService != null && logEntry.ProcessId != null)
                            logEntry.PackageName = _packageNameService.GetPackageName(logEntry.ProcessId);

                        if (logEntry.PackageName == string.Empty) continue;

                        if (_logStorageService != null) await _logStorageService.SaveLogEntryAsync(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading logcat output: {ex.Message}");
            }
        });
    }

    public void StopLogcat()
    {
        Console.WriteLine("Stopping logcat process.");
        if (_logcatProcess != null && _logcatProcess is not { HasExited: true })
        {
            _logcatProcess.Kill();
            _logcatProcess.Dispose();
            _logcatProcess = null;
            Console.WriteLine("Logcat process stopped.");
        }
        else
        {
            Console.WriteLine("Logcat process was not running.");
        }
    }

    private async Task SetAdbLogcatBuffer(string device)
    {
        var clearProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = $"-s {device} logcat -G 32M",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        clearProcess.Start();
        await clearProcess.WaitForExitAsync();
        Console.WriteLine("Logcat buffer cleared.");
    }

    private async Task ClearLogcatBuffer(string device)
    {
        var clearProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = $"-s {device} logcat -c",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        clearProcess.Start();
        await clearProcess.WaitForExitAsync();
        Console.WriteLine("Logcat buffer cleared.");
    }
}