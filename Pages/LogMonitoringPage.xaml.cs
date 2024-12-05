using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;
using WinRT.Interop;
namespace Qatalyst.Pages;

public sealed partial class LogMonitoringPage : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayedEntryCountValue => LogEntriesDisplay.Count.ToString();

    private ObservableCollection<LogEntry> LogEntriesDisplay { get; } = [];
    private ObservableCollection<string> SelectedPackages { get; set; } = [];

    private DeviceInfo? SelectedDevice { get; set; }
    private bool _isAutoScrollEnabled;

    private List<string> _availablePackages = [];
    private readonly List<int> _searchResults = [];
    private int _currentResultIndex = -1;
    private string _previousQuery = string.Empty;

    private readonly List<DateTime> _timespanData = [];

    private ContentDialog? _packageDialog;
    private DispatcherTimer? _scrollDebounceTimer;

    private readonly DispatcherQueue _dispatcherQueue;

    private readonly LogcatService? _logcatService;
    private readonly LogStorageService? _logStorageService;
    private readonly PackageNameService? _packageNameService;

    public LogMonitoringPage()
    {
        InitializeComponent();
        DataContext = this;
        LogListView.Background = ColorManager.GetBrush("AppBackgroundColor");

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _logcatService = App.Services.GetService<LogcatService>();
        _logStorageService = App.Services.GetService<LogStorageService>();
        _packageNameService = App.Services.GetService<PackageNameService>();
        var pubSubService = App.Services.GetService<PubSubService>();

        LoadPackages();

        StartStopToggleButton.Foreground = ColorManager.GetBrush("StartColor");

        LogEntriesDisplay.CollectionChanged += LogEntriesDisplayCollectionChanged;
        pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);
        pubSubService?.Subscribe("LogEntryCount", OnLogEntryCountReceived);
        pubSubService?.Subscribe("DeviceSelected", OnSelectedDeviceReceived);
        pubSubService?.Subscribe("PackageCacheInitialized", OnPackageCacheReceived);
    }

    private void LogEntriesDisplayCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DisplayedEntryCountValue));

        _ = ScrollToLatestItemAsync();
    }

    private void OnLogEntryReceived(object eventData)
    {
        if (eventData is not LogEntry { IsValid: true } logEntry) return;

        if (SelectedPackages.Any() && !SelectedPackages.Contains(logEntry.PackageName ?? string.Empty))
            return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            logEntry.GetColorForLogLevel();
            LogEntriesDisplay.Add(logEntry);
            _ = ScrollToLatestItemAsync();
        });
    }


    private void OnLogEntryCountReceived(object eventData) =>
        _dispatcherQueue.TryEnqueue(() => AllEntryCount.Text = (string)eventData);

    private void OnSelectedDeviceReceived(object eventData)
    {
        SelectedDevice = eventData as DeviceInfo;
        StartStopToggleButton.IsEnabled = SelectedDevice is not null;
    }

    private void OnPackageCacheReceived(object eventData)
    {
        if (eventData is not Tuple<List<string>, List<string>> packageCache) return;
        _availablePackages = packageCache.Item1;
        SelectedPackages = new ObservableCollection<string>(packageCache.Item2);
    }

    private async Task LoadLogEntriesIncludingPackages()
    {
        try
        {
            var selectedPackages = SelectedPackages.ToList().Count != 0
                ? SelectedPackages.ToList()
                : _availablePackages;

            if (_logStorageService == null) return;

            var logEntries = await _logStorageService.LoadLogEntriesIncludingPackagesAsync(selectedPackages);

            _dispatcherQueue.TryEnqueue(() =>
            {
                ClearLogs();
                foreach (var entry in logEntries)
                {
                    entry.GetColorForLogLevel();
                    LogEntriesDisplay.Add(entry);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading log entries: {ex.Message}");
        }
    }

    private void AutoScrollToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        _isAutoScrollEnabled = true;
    }

    private void AutoScrollToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _isAutoScrollEnabled = false;
    }

    private Task ScrollToLatestItemAsync()
    {
        if (_scrollDebounceTimer != null)
            _scrollDebounceTimer.Stop();
        else
            _scrollDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };

        _scrollDebounceTimer.Tick -= ScrollDebounceTimer_Tick; // Prevent multiple subscriptions
        _scrollDebounceTimer.Tick += ScrollDebounceTimer_Tick;

        _scrollDebounceTimer.Start();
        return Task.CompletedTask;
    }

    private void ScrollDebounceTimer_Tick(object? _, object e)
    {
        if (_scrollDebounceTimer == null) return;
            _scrollDebounceTimer.Stop();

        if (_isAutoScrollEnabled && LogEntriesDisplay.Count > 5)
        {
            LogListView.ScrollIntoView(LogEntriesDisplay.Last());
        }
    }


    private void LoadPackages()
    {
        if (_packageNameService != null) _availablePackages = _packageNameService.GetRunningPackages();
    }

    private void StartStopToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not AppBarButton button)
            return;

        var currentState = button.Tag as bool? ?? false;
        var newState = !currentState;

        UpdateButtonState(button, newState);

        HandleLogcatOperation(newState);
    }

    private void UpdateButtonState(AppBarButton button, bool newState)
    {
        button.Tag = newState;
        button.Label = newState ? "Stop" : "Start";
        StartStopIcon.Glyph = newState ? "\uE71A" : "\uE768";
        StartStopToggleButton.Foreground = ColorManager.GetBrush(
            newState ? "StopColor" : "StartColor"
        );
    }

    private async void HandleLogcatOperation(bool newState)
    {
        try
        {
            if (newState && SelectedDevice?.SerialNumber != null)
            {
                await StartLogcat(SelectedDevice.SerialNumber);
            }
            else
            {
                StopLogcat();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while handling logcat operation: {e.StackTrace}");
        }
    }

    private async Task StartLogcat(StorageFile file)
    {
        ClearLogs();
        if (_logcatService == null) return;

        await _logcatService.StartLogcat(file);
    }

    private async Task StartLogcat(string serialNumber)
    {
        ClearLogs();
        if (_logcatService != null) await _logcatService.StartLogcat(serialNumber);
    }

    private void StopLogcat()
    {
        _logcatService?.StopLogcat();
        _logStorageService?.ClearLogEntriesAsync();
    }

    private void ClearLogs()
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            LogEntriesDisplay.Clear();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => { LogEntriesDisplay.Clear(); });
        }
    }


    private void ShowPackages_Click(object sender, RoutedEventArgs e)
    {
        ShowPackageSelectionWindow();
    }

    private async void ShowPackageSelectionWindow()
    {
        try
        {
            if (_packageDialog == null)
            {
                var dialogContent = new PackageSelectionDialog();

                _packageDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    PrimaryButtonText = "Apply",
                    SecondaryButtonText = "Clear",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    Content = dialogContent
                };

                dialogContent.PopulatePackages(_availablePackages, SelectedPackages);
                dialogContent.Loaded += (_, _) =>
                {
                    dialogContent.SortPackagesBySearchTerm(string.Empty);
                };
            }

            var packageDialogContent = (PackageSelectionDialog)_packageDialog.Content;
            var result = await _packageDialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    ApplySelectedPackages(packageDialogContent);
                    break;
                case ContentDialogResult.Secondary:
                    ClearSelectedPackaged(packageDialogContent);
                    break;
                case ContentDialogResult.None:
                default:
                    return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while showing packages: {e.StackTrace}");
        }
    }

    private async void ApplySelectedPackages(PackageSelectionDialog dialogContent)
    {
        try
        {
            SelectedPackages.Clear();
            foreach (var package in dialogContent.SelectedPackages)
            {
                SelectedPackages.Add(package);
            }
            await LoadLogEntriesIncludingPackages();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error when applying selected packages: {e.StackTrace}");
        }
    }


    private void ClearSelectedPackaged(PackageSelectionDialog dialogContent)
    {
        dialogContent.ClearAllCheckboxes(SelectedPackages);
    }

    private void LogSearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = LogSearchBox.Text.ToLower();
        if (query == _previousQuery)
        {
            HandleSearchResultNavigation();
            return;
        }
        HandleSearch(query);
    }

    private void LogSearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && string.IsNullOrEmpty(sender.Text))
        {
            ClearSearchResults();
            return;
        }

        HandleSearch(sender.Text.ToLower());
    }


    private void OnNextButtonClick(object sender, RoutedEventArgs _)
    {
        HandleSearchResultNavigation();
    }

    private void OnPreviousButtonClick(object sender, RoutedEventArgs _)
    {
        HandleSearchResultNavigation(false);
    }

    private void HandleSearchResultNavigation(bool isNext = true)
    {
        if (_searchResults.Count <= 0) return;

        _currentResultIndex = isNext
            ? (_currentResultIndex + 1) % _searchResults.Count
            : _currentResultIndex = (_currentResultIndex - 1 + _searchResults.Count) % _searchResults.Count;

        ScrollToResult(_searchResults[_currentResultIndex]);
    }

    private void ScrollToResult(int index)
    {
        LogListView.SelectedIndex = index;
        LogListView.ScrollIntoView(LogListView.SelectedItem, ScrollIntoViewAlignment.Leading);
    }

    private void UpdateButtonStates()
    {
        var hasResults = _searchResults.Count != 0;
        PrevResultButton.IsEnabled = hasResults;
        NextResultButton.IsEnabled = hasResults;
        if (AutoScrollToggleButton.IsChecked == true && hasResults)
            AutoScrollToggleButton.IsChecked = false;
    }

    private void HandleSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3) return;

        ClearSearchResults();
        _currentResultIndex = -1;

        var isQuoted = (query.StartsWith('\'') && query.EndsWith('\'')) ||
                       (query.StartsWith('\"') && query.EndsWith('\"'));
        if (isQuoted)
        {
            query = query.Substring(1, query.Length - 2);
        }

        var andQueries = query.Split(["&&"], StringSplitOptions.None);
        var orQueries = query.Split(["||"], StringSplitOptions.None);

        var isAndSearch = andQueries.Length > 1;
        var isOrSearch = orQueries.Length > 1;

        var searchQueries = isAndSearch ? andQueries : orQueries;

        for (var index = 0; index < LogListView.Items.Count; index++)
        {
            if (LogListView.Items[index] is not LogEntry logEntry) continue;

            bool matches;

            if (isAndSearch)
            {
                matches = searchQueries.All(queryWord =>
                    logEntry.FormattedEntry.Contains(queryWord.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            else if (isOrSearch)
            {
                matches = searchQueries.Any(queryWord =>
                    logEntry.FormattedEntry.Contains(queryWord.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                matches = logEntry.FormattedEntry.Contains(query, StringComparison.OrdinalIgnoreCase);
            }

            if (!matches) continue;

            logEntry.BackgroundBrush = GetHighlightColor(logEntry);
            _searchResults.Add(index);
        }

        if (_searchResults.Count != 0)
        {
            _currentResultIndex = 0;
            ScrollToResult(_searchResults[_currentResultIndex]);
        }

        _previousQuery = query;

        UpdateButtonStates();
    }


    private void ClearSearchResults()
    {
        var searchResults = _searchResults
            .Where(i => i >= 0 && i < LogEntriesDisplay.Count)
            .Select(i =>  LogEntriesDisplay[i]);
        foreach (var logEntry in searchResults)
        {
            logEntry.BackgroundBrush = new SolidColorBrush(Colors.Transparent);
        }

        _searchResults.Clear();
    }


    private static SolidColorBrush GetHighlightColor(LogEntry logEntry)
    {
        var brush = logEntry.Level switch
        {
            "V" => ColorManager.GetBrush("HVerboseColor"),
            "D" => ColorManager.GetBrush("HDebugColor"),
            "I" => ColorManager.GetBrush("HInfoColor"),
            "W" => ColorManager.GetBrush("HWarningColor"),
            "E" => ColorManager.GetBrush("HErrorColor"),
            "F" => ColorManager.GetBrush("HFatalColor"),
            _ => new SolidColorBrush(Colors.Transparent)
        };

        return brush;
    }

    private void TimespanButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Parent: Grid { DataContext: LogEntry entry } } ) return;

        var dateTimeString = $"{entry.Date} {entry.Time}";
        var logEntryDateTime = DateTime.ParseExact(dateTimeString, "MM-dd HH:mm:ss.fff", null);

        _timespanData.Add(logEntryDateTime);

        PrintTimespans();
    }

    private void PrintTimespans()
    {
        if (_timespanData.Count < 2)
            return;

        var prevDateTime = _timespanData[0];
        var currDateTime = _timespanData[^1];

        var timespan = currDateTime.Subtract(prevDateTime);
        TimespanTextBlock.Text = $@"{timespan:hh\:mm\:ss\.ffff}";
    }

    private void ClearTimespanButton_OnClick(object sender, RoutedEventArgs e)
    {
        _timespanData.Clear();

        for (var i = 0; i < LogListView.Items.Count; i++)
        {
            var listViewItem = LogListView.ContainerFromIndex(i) as ListViewItem;
            if (listViewItem == null) continue;

            FindTimespanButton(listViewItem, out var timespanButton);
            if (timespanButton == null) continue;
            timespanButton.IsChecked = false;
        }

        TimespanTextBlock.Text = "00:00:00.0000";
    }

    private static void FindTimespanButton(DependencyObject obj, out ToggleButton? button)
    {
        button = null;
        var childrenCount = VisualTreeHelper.GetChildrenCount(obj);
        for (var i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is ToggleButton { Name: "TimespanButton" } toggleButton)
            {
                button = toggleButton;
                return;
            }
            FindTimespanButton(child, out button);
        }
    }

    private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not AppBarButton button) return;
            var isUnfilteredExport = button.Tag.ToString() == "UNFILTERED_EXPORT";
            var isJsonFormat = button.Name switch
            {
                var name when name == ExportJsonButton.Name => true,
                var name when name == ExportUnfilteredJsonButton.Name => true,
                var name when name == ExportFormattedButton.Name => false,
                var name when name == ExportUnfilteredFormattedButton.Name => false,
                _ => false
            };

            var defaultFileExtension = isJsonFormat ? ".logcat" : ".txt";
            var suggestedFileName = (isUnfilteredExport ? "LOGCAT_UNFILTERED_" : "LOGCAT_")
                                    + DateTime.Now.ToString("yyyyMMddHHmmss")
                                    + defaultFileExtension;


            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName,
                DefaultFileExtension = defaultFileExtension
            };

            // Add file types to save picker
            savePicker.FileTypeChoices.Add("logcat File", new List<string> { defaultFileExtension });

            var window = App.MainAppWindow;
            var hWnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(savePicker, hWnd);

            var file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                if (isUnfilteredExport && _logStorageService != null)
                    await _logStorageService.ExportFile(file.Path, isJsonFormat);
                else
                    await ExportFile(file, isJsonFormat);

                Console.WriteLine("File saved successfully.");

                // Show confirmation dialog
                var dialog = new ContentDialog
                {
                    Title = "Export Successful",
                    Content =
                        "The file has been saved successfully. Do you want to open the directory where the file was exported?",
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = App.MainAppWindow.Content.XamlRoot // Ensure correct XamlRoot for ContentDialog
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // Open the directory containing the file
                    var folderPath = Path.GetDirectoryName(file.Path);
                    if (folderPath != null)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            else
            {
                Console.WriteLine("Save operation was cancelled.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving file: " + ex.Message);
        }
    }

    private async void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker
            {
                FileTypeFilter = { ".logcat" },
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            var window = App.MainAppWindow;

            var hWnd = WindowNative.GetWindowHandle(window);

            InitializeWithWindow.Initialize(openPicker, hWnd);

            var file = await openPicker.PickSingleFileAsync();

            await StartLogcat(file);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading file: " + ex.Message);
        }
    }


    private Task ExportFile(StorageFile file, bool isJsonFormat)
    {
        _ = isJsonFormat ? ExportToJsonFile(file) : ExportToTxtFile(file);
        return Task.CompletedTask;
    }

    private async Task ExportToJsonFile(StorageFile file)
    {
        var logEntries = LogEntriesDisplay.ToList();
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
            entry.PackageName
        });

        var jsonContent = JsonConvert.SerializeObject(filteredEntries, Formatting.Indented);

        // Use asynchronous file write
        await FileIO.WriteTextAsync(file, jsonContent);
    }


    private async Task ExportToTxtFile(StorageFile file)
    {
        var logEntries = LogEntriesDisplay.ToList();
        var formattedEntries = logEntries.Select(entry => entry.FormattedEntry);

        await FileIO.WriteLinesAsync(file, formattedEntries);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}