using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace Qatalyst.Objects;

public partial class LogEntry : INotifyPropertyChanged
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
    public SolidColorBrush TextBrush { get; set; }
    public bool IsChecked { get; set; } = false;
    public bool IsValid { get; set; } = true;


    private SolidColorBrush _backgroundBrush;

    public SolidColorBrush BackgroundBrush
    {
        get => _backgroundBrush;
        set
        {
            if (_backgroundBrush != value)
            {
                _backgroundBrush = value;
                OnPropertyChanged();
            }
        }
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
            ? value[^length..]
            : value.PadRight(length);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}