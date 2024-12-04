using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Qatalyst.Objects;

namespace Qatalyst.Services
{
    public class LogStorageService
    {
        private static int _currentId = 0;
        private readonly ConcurrentDictionary<int, LogEntry> _logEntries = new();
        private readonly PubSubService _pubSubService;

        private const string LogEntrySavedEvent = "LogEntrySaved";
        private const string LogEntryCountEvent = "LogEntryCount";

        public LogStorageService()
        {
            _pubSubService = App.Services.GetService<PubSubService>();
        }

        public Task SaveLogEntryAsync(LogEntry entry)
        {
            if (entry == null) return Task.CompletedTask;

            entry.Id = Interlocked.Increment(ref _currentId);
            _logEntries[entry.Id] = entry;

            _pubSubService.Publish(LogEntrySavedEvent, entry);
            _pubSubService.Publish(LogEntryCountEvent, _logEntries.Count.ToString());

            return Task.CompletedTask;
        }

        public Task<List<LogEntry>> LoadLogEntriesIncludingPackagesAsync(List<string> includedPackageNames)
        {
            if (includedPackageNames == null || includedPackageNames.Count == 0)
                throw new ArgumentException("Included package names cannot be null or empty.", nameof(includedPackageNames));

            var packageSet = new HashSet<string>(includedPackageNames, StringComparer.OrdinalIgnoreCase);
            var matchingEntries = _logEntries.Values
                .Where(entry => !string.IsNullOrEmpty(entry.PackageName) &&
                                packageSet.Contains(entry.PackageName))
                .ToList();

            Console.WriteLine($"Found {matchingEntries.Count} matching log entries");
            return Task.FromResult(matchingEntries);
        }

        public Task ClearLogEntriesAsync()
        {
            _logEntries.Clear();
            return Task.CompletedTask;
        }

        public Tuple<double, string> GetCurrentMemoryUsage()
        {
            var process = Process.GetCurrentProcess();
            var currentMemory = process.WorkingSet64;
            var gcInfo = GC.GetGCMemoryInfo();

            var memoryUsagePercentage = (currentMemory / (double)gcInfo.HighMemoryLoadThresholdBytes) * 100;
            var readableCurrent = GetReadableSize(currentMemory);
            var readableThreshold = GetReadableSize(gcInfo.HighMemoryLoadThresholdBytes);

            var usageString = $"Approx. Memory Usage: {readableCurrent}/{readableThreshold} ({memoryUsagePercentage:F2}%)";
            return new Tuple<double, string>(memoryUsagePercentage, usageString);
        }

        public async Task ExportFile(string filename, bool isJsonFormat)
        {
            if (isJsonFormat)
                await ExportToJsonFile(filename);
            else
                await ExportToTxtFile(filename);
        }

        private Task ExportToTxtFile(string filePath)
        {
            var logEntries = _logEntries.Values.ToList();

            // Extract the FormattedEntry of each LogEntry
            var formattedEntries = logEntries.Select(entry => entry.FormattedEntry);

            // Write all formatted entries to the file, each on a new line
            File.WriteAllLines(filePath, formattedEntries);

            return Task.CompletedTask;
        }

        private Task ExportToJsonFile(string filePath)
        {
            var logEntries = _logEntries.Values.ToList();
            var filteredEntries = logEntries.Select(entry => new
            {
                entry.Id,
                entry.Date,
                entry.Time,
                entry.ProcessId,
                entry.ThreadId,
                entry.Level,
                entry.Tag,
                entry.Message,
                entry.PackageName,
                entry.IsValid
            });

            // Serialize the filtered entries to JSON
            var jsonContent = JsonConvert.SerializeObject(filteredEntries, Formatting.Indented);

            // Write the JSON content to the specified file
            File.WriteAllText(filePath, jsonContent);

            return Task.CompletedTask;
        }

        private static string GetReadableSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F2} GB",
                >= MB => $"{bytes / (double)MB:F2} MB",
                >= KB => $"{bytes / (double)KB:F2} KB",
                _ => $"{bytes} Bytes"
            };
        }
    }
}
