using System;
using System.ComponentModel.DataAnnotations.Schema;
using InLoVe.Utils;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace InLoVe.Objects;

public class LogEntry
{
    public int Id { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public string? ProcessId { get; set; }
    public string? ThreadId { get; set; }
    public string? Level { get; set; }
    public string? Tag { get; set; }
    public string? Message { get; set; }
    public string? PackageName { get; set; }

    // Should not be saved to DB

    [NotMapped]
    public SolidColorBrush Color { get; private set; }

    [NotMapped]
    public bool IsValid { get; private set; }

    public static LogEntry? CreateFromLogLine(string logLine)
    {
        var entry = new LogEntry();
        entry.IsValid = entry.ParseLogLine(logLine);

        if (entry.IsValid)
        {
            entry.Color = GetColorForLogLevel(entry.Level);
            return entry;
        }

        return null;
    }

    private bool ParseLogLine(string logLine)
    {
        var parts = logLine.Split([' '], 7, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 7)
        {
            Console.WriteLine($"Log line format is invalid.[{logLine}]");
            return false;
        }

        Date = parts[0];
        Time = parts[1];
        ProcessId = parts[2];
        ThreadId = parts[3];
        Level = parts[4];
        Tag = parts[5].TrimEnd(':');
        Message = parts[6];
        return true;
    }

    public string FormattedEntry =>
        $"{Date} " +
        $"{Time} " +
        $"{GetTrimmedOrPadded(FormatPackageName(), 40)} " +
        $"{Level} " +
        $"{GetTrimmedOrPadded(Tag, 48)} " +
        $"{(Message != null && Message.StartsWith(": ") ? Message : ": " + Message)}";
    
    private string FormatPackageName()
    {
        var truncatedPackageName = PackageName.Length >= 32 ? PackageName[^32..] : PackageName;
        return $"{truncatedPackageName} ({ProcessId})";
    }

    private static string GetTrimmedOrPadded(string value, int length)
    {
        if (string.IsNullOrEmpty(value))
            return "".PadRight(length);

        return value.Length > length
            ? value.Substring(value.Length - length)
            : value.PadRight(length);
    }

    private static SolidColorBrush GetColorForLogLevel(string level)
    {
        return level switch
        {
            "V" => ColorManager.GetBrush(AppColor.VerboseColor.ToString()),
            "D" => ColorManager.GetBrush(AppColor.DebugColor.ToString()),
            "I" => ColorManager.GetBrush(AppColor.InfoColor.ToString()),
            "W" => ColorManager.GetBrush(AppColor.WarningColor.ToString()),
            "E" => ColorManager.GetBrush(AppColor.ErrorColor.ToString()),
            "F" => ColorManager.GetBrush(AppColor.FatalColor.ToString()),
            _ => new SolidColorBrush(Colors.Black),
        };
    }
}