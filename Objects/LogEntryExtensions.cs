using System;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Qatalyst.Utils;

namespace Qatalyst.Objects;

public static class LogEntryExtensions
{
    public static LogEntry? CreateFromLogLine(this string logLine)
    {
        var entry = new LogEntry();
        entry.IsValid = ParseLogLine(entry, logLine);

        return !entry.IsValid ? null : entry;
    }

    private static bool ParseLogLine(LogEntry entry, string logLine)
    {
        var parts = logLine.Split([' '], 7, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 7)
        {
            return false;
        }

        entry.Date = parts[0];
        entry.Time = parts[1];
        entry.ProcessId = parts[2];
        entry.ThreadId = parts[3];
        entry.Level = parts[4];
        entry.Tag = parts[5].TrimEnd(':');
        entry.Message = parts[6];
        return true;
    }

    public static string FormatPackageName(this LogEntry entry)
    {
        var truncatedPackageName = entry.PackageName.Length >= 32
            ? entry.PackageName[^32..]
            : entry.PackageName;
        return $"{truncatedPackageName} ({entry.ProcessId})";
    }

    public static int GetSize(this LogEntry entry)
    {
        return sizeof(int) * 2
               + Encoding.UTF8.GetByteCount(entry.Date)
               + Encoding.UTF8.GetByteCount(entry.Time)
               + Encoding.UTF8.GetByteCount(entry.ProcessId)
               + Encoding.UTF8.GetByteCount(entry.ThreadId)
               + Encoding.UTF8.GetByteCount(entry.Level)
               + Encoding.UTF8.GetByteCount(entry.Tag)
               + Encoding.UTF8.GetByteCount(entry.PackageName)
               + Encoding.UTF8.GetByteCount(entry.Message);
    }

    public static string GetTrimmedOrPadded(this string value, int length)
    {
        if (string.IsNullOrEmpty(value))
            return "".PadRight(length);

        return value.Length > length
            ? value[^length..]
            : value.PadRight(length);
    }

    public static void GetColorForLogLevel(this LogEntry entry)
    {
        try
        {
            var brush = entry.Level switch
            {
                "V" => ColorManager.GetBrush("VerboseColor"),
                "D" => ColorManager.GetBrush("DebugColor"),
                "I" => ColorManager.GetBrush("InfoColor"),
                "W" => ColorManager.GetBrush("WarningColor"),
                "E" => ColorManager.GetBrush("ErrorColor"),
                "F" => ColorManager.GetBrush("FatalColor"),
                _ => new SolidColorBrush(Colors.Black),
            };
            entry.TextBrush = brush;
            entry.BackgroundBrush = new SolidColorBrush(Colors.Transparent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for log level {entry.Level}: {ex.Message}");
        }
    }
}