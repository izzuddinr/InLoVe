using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public sealed partial class LogMonitoringPage
{
    private ObservableCollection<LogEntry> LogEntriesDisplay { get; } = [];
    private ObservableCollection<string> SelectedPackages { get; set; } = [];

    private DeviceInfo? SelectedDevice { get; set; }
    private bool _isAutoScrollEnabled;

    private List<string> _availablePackages = [];
    private List<int> _searchResults = [];
    private int _currentResultIndex = -1;
    private string _previousQuery = string.Empty;

    private List<int> _timespanIndices = [];
    private List<DateTime> _timespanData = [];

    private ContentDialog? _packageDialog;
    private DispatcherTimer? _scrollDebounceTimer;

    private readonly DispatcherQueue _dispatcherQueue;

    private readonly LogcatService? _logcatService;
    private readonly LogStorageService? _logStorageService;
    private readonly PackageNameService? _packageNameService;
    private readonly PubSubService? _pubSubService;

    public LogMonitoringPage()
    {
        InitializeComponent();
        DataContext = this;
        LogListView.Background = ColorManager.GetBrush("AppBackgroundColor");

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _logcatService = App.Services.GetService<LogcatService>();
        _logStorageService = App.Services.GetService<LogStorageService>();
        _packageNameService = App.Services.GetService<PackageNameService>();
        _pubSubService = App.Services.GetService<PubSubService>();

        LoadPackages();

        StartStopToggleButton.Foreground = ColorManager.GetBrush("StartColor");

        LogEntriesDisplay.CollectionChanged += LogEntriesDisplayCollectionChanged;
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);
        _pubSubService?.Subscribe("LogEntryCount", OnLogEntryCountReceived);
        _pubSubService?.Subscribe("DeviceSelected", OnSelectedDeviceReceived);
        _pubSubService?.Subscribe("PackageCacheInitialized", OnPackageCacheReceived);
    }

    private async void LogEntriesDisplayCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null) return;

        foreach (var newEntry in e.NewItems.Cast<LogEntry>())
        {
            if (!newEntry.IsValid) continue;

            if (SelectedPackages.Any() && newEntry.PackageName != null && !SelectedPackages.Contains(newEntry.PackageName))
                continue;

            _dispatcherQueue.TryEnqueue(async void () =>
            {
                LogListView.Items.Add(newEntry);
                DisplayedEntryCount.Text = LogEntriesDisplay.Count.ToString();
                await Task.Yield(); // Allow asynchronous execution if necessary
            });
        }

        await ScrollToLatestItemAsync();
    }

    private void OnLogEntryReceived(object eventData)
    {
        var logEntry = eventData as LogEntry;

        if (logEntry is { IsValid: false }) return;

        if (SelectedPackages.Any() && !SelectedPackages.Contains(logEntry?.PackageName ?? string.Empty))
            return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (logEntry == null) return;

            logEntry.GetColorForLogLevel();
            LogEntriesDisplay.Add(logEntry);
            ScrollToLatestItemAsync();
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

    private async void LoadLogEntriesIncludingPackages()
    {
        try
        {
            var selectedPackages = SelectedPackages.ToList().Count != 0
                ? SelectedPackages.ToList()
                : _availablePackages;

            if (_logStorageService == null) return;

            var logEntries = await _logStorageService.LoadLogEntriesIncludingPackagesAsync(selectedPackages);

            Console.WriteLine($"Loaded {logEntries.Count} matching log entries");
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

        _scrollDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200) // Set debounce delay
        };

        _scrollDebounceTimer.Tick += (s, e) =>
        {
            _scrollDebounceTimer.Stop();
            if (_isAutoScrollEnabled && LogEntriesDisplay.Count > 0 && LogListView.Items.Count > 5)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    LogListView.ScrollIntoView(LogEntriesDisplay.Last());
                });
            }
        };

        // Restart the debounce timer on every call
        _scrollDebounceTimer.Stop();
        _scrollDebounceTimer.Start();
        return Task.CompletedTask;
    }


    private void LoadPackages()
    {
        if (_packageNameService != null) _availablePackages = _packageNameService.GetRunningPackages();
    }

    private void StartStopToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not AppBarButton button) return;

        var currentState = button.Tag is bool tagValue && tagValue;
        var newState = !currentState;

        Console.WriteLine($"Current state is {currentState}, newState is {newState}");

        _dispatcherQueue.TryEnqueue(() =>
        {
            button.Tag = newState;
            button.Label = newState ? "Stop" : "Start";
            StartStopIcon.Glyph = newState ? "\uE71A" : "\uE768";
            StartStopToggleButton.Foreground = newState
                ? ColorManager.GetBrush("StopColor")
                : ColorManager.GetBrush("StartColor");
        });

        if (newState)
            StartLogcat(SelectedDevice.SerialNumber);
        else
            StopLogcat();
    }

    private void StartStopToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
    }

    private async void StartLogcat(string serialNumber)
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
        _dispatcherQueue.TryEnqueue(() =>
        {
            LogListView.Items.Clear();
            LogEntriesDisplay.Clear();
        });
    }

    private void ShowPackages_Click(object sender, RoutedEventArgs e)
    {
        ShowPackageSelectionWindow();
    }

    private async void ShowPackageSelectionWindow()
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

    private void ApplySelectedPackages(PackageSelectionDialog dialogContent)
    {
        SelectedPackages.Clear();
        foreach (var package in dialogContent.SelectedPackages)
        {
            SelectedPackages.Add(package);
        }
        LoadLogEntriesIncludingPackages();
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
        var hasResults = _searchResults.Any();
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

        var isQuoted = (query.StartsWith("'") && query.EndsWith("'")) ||
                       (query.StartsWith("\"") && query.EndsWith("\""));
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

            var matches = false;

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

        if (_searchResults.Any())
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


    private SolidColorBrush GetHighlightColor(LogEntry logEntry)
    {
        var brush = logEntry.Level switch
        {
            "V" => ColorManager.GetBrush("HVerboseColor"),
            "D" => ColorManager.GetBrush("HDebugColor"),
            "I" => ColorManager.GetBrush("HInfoColor"),
            "W" => ColorManager.GetBrush("HWarningColor"),
            "E" => ColorManager.GetBrush("HErrorColor"),
            "F" => ColorManager.GetBrush("HFatalColor"),
            _ => new SolidColorBrush(Colors.Transparent),
        };

        return brush;
    }

    private void TimespanButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Get the ToggleButton that was clicked
        var toggleButton = sender as ToggleButton;

        // Get the parent Grid that contains the DataContext
        var grid = toggleButton.Parent as Grid;

        // Get the LogEntry from the DataContext
        var logEntry = grid.DataContext as LogEntry;

        // Get the index of the LogEntry in the ListView
        var index = LogListView.Items.IndexOf(logEntry);

        // Parse the date and time string to a DateTime object
        var dateTimeString = $"{logEntry.Date} {logEntry.Time}";
        var logEntryDateTime = DateTime.ParseExact(dateTimeString, "MM-dd HH:mm:ss.fff", null);

        // Add the DateTime to the list
        _timespanData.Add(logEntryDateTime);
        _timespanIndices.Add(index);

        // Print the timespans
        PrintTimespans();
    }

    private void PrintTimespans()
    {
        if (_timespanData.Count < 2)
            return;

        var prevDateTime = _timespanData[0];
        var currDateTime = _timespanData[^1];

        TimeSpan timespan = currDateTime.Subtract(prevDateTime);
        TimespanTextBlock.Text = $"{timespan:hh\\:mm\\:ss\\.ffff}";
    }

    private void ClearTimespanButton_OnClick(object sender, RoutedEventArgs e)
    {
        _timespanData.Clear();
        _timespanIndices.Clear();

        // Loop through all the ListViewItems and set the TimespanButton IsChecked to false
        for (var i = 0; i < LogListView.Items.Count; i++)
        {
            var listViewItem = LogListView.ContainerFromIndex(i) as ListViewItem;
            if (listViewItem == null) continue;

            FindTimespanButton(listViewItem, out var timespanButton);
            timespanButton.IsChecked = false;
        }

        // Reset the Timespan TextBlock
        TimespanTextBlock.Text = "00:00:00.0000";
    }

    private void FindTimespanButton(DependencyObject obj, out ToggleButton button)
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
}