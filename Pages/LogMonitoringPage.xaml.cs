using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public sealed partial class LogMonitoringPage
{
    public ObservableCollection<LogEntry> LogEntriesDisplay { get; private set; } = [];
    public ObservableCollection<LogEntry> LogEntriesStorage { get; private set; } = [];

    private ObservableCollection<string> SelectedPackages { get; set; } = [];


    private List<string> _availablePackages = [];
    private ContentDialog? _packageDialog;

    private List<int> _searchResults = [];
    private int _currentResultIndex = -1;
    private string _previousQuery = string.Empty;

    public string SelectedDevice { get; set; } = string.Empty;

    private readonly DispatcherQueue _dispatcherQueue;

    private readonly DeviceService? _deviceService;
    private readonly LogcatService? _logcatService;
    private readonly LogStorageService? _logStorageService;
    private readonly PackageNameService? _packageNameService;
    private readonly PubSubService? _pubSubService;
    private bool _isAutoScrollEnabled = false;

    private DispatcherTimer _scrollDebounceTimer;


    public LogMonitoringPage()
    {
        InitializeComponent();
        DataContext = this;
        LogListView.Background = ColorManager.GetBrush("AppBackgroundColor");

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _logcatService = App.Services.GetService<LogcatService>();
        _logStorageService = App.Services.GetService<LogStorageService>();
        _deviceService = App.Services.GetService<DeviceService>();
        _packageNameService = App.Services.GetService<PackageNameService>();
        _pubSubService = App.Services.GetService<PubSubService>();

        LoadDevices();
        LoadPackages();

        StartStopToggleButton.Background = ColorManager.GetBrush("StartColor");
        StartStopToggleButton.IsEnabled = DeviceComboBox.SelectedIndex != -1;

        LogEntriesDisplay.CollectionChanged += LogEntriesDisplayCollectionChanged;
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);
        _pubSubService?.Subscribe("PackageCacheInitialized", OnPackageCacheReceived);
    }

    private void LogEntriesDisplayCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null) return;

        foreach (LogEntry newEntry in e.NewItems)
        {
            if (!newEntry.IsValid) continue;

            if (SelectedPackages.Any() && newEntry.PackageName != null && !SelectedPackages.Contains(newEntry.PackageName))
                continue;

            _dispatcherQueue.TryEnqueue(() =>
            {
                LogListView.Items.Add(newEntry);
                LogEntriesDisplayCount.Text = $"{LogListView.Items.Count} entries";
            });
        }

        ScrollToLatestItem();
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
            ScrollToLatestItem();
        });
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
            var selectedPackages = SelectedPackages.ToList().Any() ? SelectedPackages.ToList() : _availablePackages;
            if (_logStorageService == null) return;

            var logEntries = await _logStorageService.LoadLogEntriesIncludingPackagesAsync(selectedPackages);

            Console.WriteLine($"Loaded {logEntries.Count} matching log entries");
            _dispatcherQueue.TryEnqueue(() =>
            {
                ClearLogs();
                foreach (var entry in logEntries)
                {
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

    private void ScrollToLatestItem()
    {
        if (_scrollDebounceTimer == null)
        {
            _scrollDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // Set debounce delay
            };
            _scrollDebounceTimer.Tick += (s, e) =>
            {
                _scrollDebounceTimer.Stop();

                // Perform the scroll operation
                if (_isAutoScrollEnabled && LogEntriesDisplay.Count > 0 && LogListView.Items.Count > 5)
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        LogListView.ScrollIntoView(LogEntriesDisplay.Last());
                    });
                }
            };
        }

        // Restart the debounce timer on every call
        _scrollDebounceTimer.Stop();
        _scrollDebounceTimer.Start();
    }

    private void LoadPackages()
    {
        if (_packageNameService != null) _availablePackages = _packageNameService.GetRunningPackages();
    }

    private async void LoadDevices()
    {
        try
        {
            if (_deviceService == null) return;

            DeviceComboBox.Items.Clear();

            Console.WriteLine("Loading devices...");
            var devices = await _deviceService.GetConnectedDevices();
            foreach (var device in devices)
            {
                DeviceComboBox.Items.Add(device);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading devices: {ex.Message}");
        }
    }

    private void StartStopToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        if (DeviceComboBox.SelectedIndex < 0) return;
        StartStopIcon.Glyph = "\uE71A";
        StartStopText.Text = "Stop";
        StartStopToggleButton.Background = ColorManager.GetBrush("StopColor");
        DeviceComboBox.IsEnabled = false;
        LoadDeviceButton.IsEnabled = false;
        StartLogcat();
    }

    private void StartStopToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        StartStopIcon.Glyph = "\uE768";
        StartStopText.Text = "Start";
        StartStopToggleButton.Background = ColorManager.GetBrush("StartColor");
        DeviceComboBox.IsEnabled = true;
        LoadDeviceButton.IsEnabled = true;
        StopLogcat();
    }

    private async void StartLogcat()
    {
        ClearLogs();
        if (_logcatService != null) await _logcatService.StartLogcat(SelectedDevice);
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


    private void DeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StartStopToggleButton.IsEnabled = DeviceComboBox.SelectedIndex != -1;

        if (DeviceComboBox.SelectedItem is not string selectedDevice) return;

        Console.WriteLine($"Device selected: {selectedDevice}");
        SelectedDevice = selectedDevice;
        _pubSubService?.Publish("DeviceSelected", selectedDevice);
        _packageDialog = null;
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

    private void RefreshDevice_Click(object sender, RoutedEventArgs e)
    {
        if (!DeviceComboBox.IsEnabled) return;

        LoadDevices();
    }

    private void LogSearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = LogSearchBox.Text.ToLower();
        if (query == _previousQuery)
        {
            HandleSearchResultNavigation();
            return;
        }

        _searchResults.Clear();
        _currentResultIndex = -1;

        if (!string.IsNullOrEmpty(query))
        {
            for (int index = 0; index < LogListView.Items.Count; index++)
            {
                if (LogListView.Items[index] is LogEntry logEntry &&
                    logEntry.FormattedEntry.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    _searchResults.Add(index);
                }
            }

            if (_searchResults.Any())
            {
                _currentResultIndex = 0;
                ScrollToResult(_searchResults[_currentResultIndex]);
            }
        }

        _previousQuery = query;

        UpdateButtonStates();
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
    }
}