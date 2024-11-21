using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using InLoVe.Objects;
using Microsoft.Extensions.DependencyInjection;

namespace InLoVe.Services;

public class LogStorageService
{
    private static int _currentId = 0;
    private readonly ConcurrentDictionary<int, LogEntry> _logEntries = new();
    private readonly PubSubService _pubSubService;

    public LogStorageService()
    {
        _pubSubService = App.Services.GetService<PubSubService>();
    }

    public Task SaveLogEntryAsync(LogEntry entry)
    {
        entry.Id = System.Threading.Interlocked.Increment(ref _currentId);
        _logEntries[entry.Id] = entry;
        _pubSubService.Publish("LogEntrySaved", entry);
        return Task.CompletedTask;
    }

    public Task<List<LogEntry>> LoadLogEntriesIncludingPackagesAsync(List<string> includedPackageNames)
    {
        var logEntries = _logEntries.Values
            .Where(entry => includedPackageNames.Contains(entry.PackageName))
            .ToList();
        return Task.FromResult(logEntries);
    }

    public Task ClearLogEntriesAsync()
    {
        _logEntries.Clear();
        return Task.CompletedTask;
    }

    public Tuple<double, string> GetCurrentMemoryUsage()
    {
        var currentMemoryUsage = GetReadableSize(Process.GetCurrentProcess().WorkingSet64);
        var availableMemory = GetReadableSize(GC.GetGCMemoryInfo().HighMemoryLoadThresholdBytes);

        var memoryUsagePercentage = (Process.GetCurrentProcess().WorkingSet64 / (double)GC.GetGCMemoryInfo().HighMemoryLoadThresholdBytes) * 100;

        return new Tuple<double, string>(memoryUsagePercentage, $"Approx. Memory Usage: {currentMemoryUsage}/{availableMemory} ({memoryUsagePercentage:F2}%)");
    }

    private static string GetReadableSize(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2}GB",
            >= MB => $"{bytes / (double)MB:F2}MB",
            >= KB => $"{bytes / (double)KB:F2}KB",
            _ => $"{bytes} Bytes"
        };
    }

}