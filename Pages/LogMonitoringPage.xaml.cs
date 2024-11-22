using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using InLoVe.Objects;
using InLoVe.Services;
using InLoVe.Utils;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace InLoVe.Pages;

public sealed partial class LogMonitoringPage
{
    public ObservableCollection<string> Devices { get; private set; } = new() { };
    public ObservableCollection<LogEntry> LogEntries { get; private set; } = new() { };

    private ObservableCollection<string> SelectedPackages { get; set; } = new() { };


    private List<string> _availablePackages = [];
    private ContentDialog? _packageDialog;

    public string SelectedDevice { get; set; } = string.Empty;

    private readonly DispatcherQueue _dispatcherQueue;

    private readonly DeviceService? _deviceService;
    private readonly LogcatService? _logcatService;
    private readonly LogStorageService? _logStorageService;
    private readonly PackageNameService? _packageNameService;
    private readonly PubSubService? _pubSubService;


    public LogMonitoringPage()
    {
        InitializeComponent();
        DataContext = this;
        LogScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _logcatService = App.Services.GetService<LogcatService>();
        _logStorageService = App.Services.GetService<LogStorageService>();
        _deviceService = App.Services.GetService<DeviceService>();
        _packageNameService = App.Services.GetService<PackageNameService>();
        _pubSubService = App.Services.GetService<PubSubService>();

        LoadDevices();
        LoadPackages();

        StartStopToggleButton.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());
        StartStopToggleButton.IsEnabled = DeviceComboBox.SelectedIndex != -1;

        LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);
        _pubSubService?.Subscribe("PackageCacheInitialized", OnPackageCacheReceived);
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

            AppendTextToLog(logEntry);

            if (AutoScrollToggleButton.IsChecked == true)
            {
                ScrollToBottom();
            }
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

            _dispatcherQueue.TryEnqueue(() =>
            {
                ClearLogs();
                foreach (var entry in logEntries)
                {
                    Console.WriteLine($"Added log entry to collection: {entry.FormattedEntry}");
                    AppendTextToLog(entry);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading log entries: {ex.Message}");
        }
    }

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null) return;

        foreach (LogEntry newEntry in e.NewItems)
        {
            if (!newEntry.IsValid) continue;

            if (SelectedPackages.Any() && newEntry.PackageName != null && !SelectedPackages.Contains(newEntry.PackageName))
                continue;

            _dispatcherQueue.TryEnqueue(() =>
            {
                AppendTextToLog(newEntry);

                if (AutoScrollToggleButton.IsChecked == true)
                {
                    ScrollToBottom();
                }
            });
        }
    }

    private async void LoadDevices()
    {
        try
        {
            Console.WriteLine("Loading devices...");
            if (_deviceService == null) return;

            var devices = await _deviceService.GetConnectedDevices();
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
                DeviceComboBox.Items.Add(device);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading devices: {ex.Message}");
        }
    }

    private void LoadPackages()
    {
        if (_packageNameService != null) _availablePackages = _packageNameService.GetRunningPackages();
    }

    private void StartStopToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        StartStopIcon.Glyph = "\uE71A";
        StartStopText.Text = "Stop";
        StartStopToggleButton.Background = ColorManager.GetBrush(AppColor.StopColor.ToString());
        DeviceComboBox.IsEnabled = false;
        StartLogcat();
    }

    private void StartStopToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        StartStopIcon.Glyph = "\uE768";
        StartStopText.Text = "Start";
        StartStopToggleButton.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());
        DeviceComboBox.IsEnabled = true;
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
    }

    private void AppendTextToLog(LogEntry logEntry)
    {
        var paragraph = LogTextBlock.Blocks[0] as Paragraph;
        if (paragraph != null)
        {
            var run = new Run
            {
                Text = logEntry.FormattedEntry + "\n",
                Foreground = logEntry.Color
            };
            paragraph.Inlines.Add(run);
        }
    }

    private void ClearLogs()
    {
        var paragraph = LogTextBlock.Blocks[0] as Paragraph;
        paragraph?.Inlines.Clear();
    }

    private void ScrollToBottom()
    {
        LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null, true);
    }

    private void DeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StartStopToggleButton.IsEnabled = DeviceComboBox.SelectedIndex != -1;

        if (DeviceComboBox.SelectedItem is not string selectedDevice) return;

        Console.WriteLine($"Device selected: {selectedDevice}");
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
        ClearLogs();
        LoadLogEntriesIncludingPackages();
    }

    private void ClearSelectedPackaged(PackageSelectionDialog dialogContent)
    {
        dialogContent.ClearAllCheckboxes(SelectedPackages);
    }
}