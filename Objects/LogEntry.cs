using System;
using System.Text;
using Microsoft.UI.Xaml.Media;
using Qatalyst.Utils;

namespace Qatalyst.Objects;

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
    public SolidColorBrush Color { get; set; }
    public bool IsValid { get; set; }
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
            ? value[^length..]
            : value.PadRight(length);
    }
}