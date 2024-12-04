using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Qatalyst.Objects;
using Qatalyst.Services;

namespace Qatalyst;

public sealed partial class MainWindow : Window
{

    private readonly LogStorageService _logStorageService = App.Services.GetRequiredService<LogStorageService>();
    private readonly Dictionary<string, Frame> _pageFrames = new();
    private readonly PubSubService _pubSubService = App.Services.GetRequiredService<PubSubService>();

    private DispatcherTimer _memoryUpdateTimer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeDirectories();
        InitializeFrames();
        StartMemoryUsageUpdate();
    }

    private void InitializeFrames()
    {
        _pageFrames["HomePage"] = CreateFrame(typeof(Pages.HomePage));
        _pageFrames["LogMonitoringPage"] = CreateFrame(typeof(Pages.LogMonitoringPage));
        _pageFrames["DevicePage"] = CreateFrame(typeof(Pages.DevicePage));
        _pageFrames["Iso8583ParsingPage"] = CreateFrame(typeof(Pages.Iso8583ParsingPage));
        _pageFrames["HostRecordPage"] = CreateFrame(typeof(Pages.HostRecordPage));

        ContentPresenter.Content = _pageFrames["HomePage"];
        _pubSubService?.Subscribe("DeviceSelected", OnSelectedDeviceReceived);
        Closed += OnWindowClosed;
    }

    private void OnSelectedDeviceReceived(object eventData)
    {
        if (eventData is not DeviceInfo deviceInfo) return;

        SelectedDeviceText.Text = $"{deviceInfo.SerialNumber} ({deviceInfo.Manufacturer} - {deviceInfo.Model})" ?? "-";
    }

    private void InitializeDirectories()
    {
        var screenshotsDirectory = $@"{Environment.CurrentDirectory}\Screenshots";

        if (!Directory.Exists(screenshotsDirectory))
        {
            Console.WriteLine($"Creating screenshots directory: {screenshotsDirectory}");
            Directory.CreateDirectory(screenshotsDirectory);
        }
    }


    private Frame CreateFrame(Type pageType)
    {
        var frame = new Frame();
        frame.Navigate(pageType);
        return frame;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;

        if (args.IsSettingsSelected)
        {
            ContentPresenter.Content = null;
            ContentPresenter.Content = CreateFrame(typeof(Pages.SettingsPage));
        }

        if (_pageFrames.TryGetValue(tag, out var value))
        {
            ContentPresenter.Content = value;
        }
    }

    private void StartMemoryUsageUpdate()
    {
        if (_memoryUpdateTimer == null)
        {
            _memoryUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _memoryUpdateTimer.Tick += (sender, e) => UpdateInfoBar();
        }

        _memoryUpdateTimer.Start();
    }

    public void StopMemoryUsageUpdate()
    {
        if (_memoryUpdateTimer == null || !_memoryUpdateTimer.IsEnabled) return;
        _memoryUpdateTimer.Stop();
        _memoryUpdateTimer.Tick -= (sender, e) => UpdateInfoBar();
    }

    private void UpdateInfoBar()
    {
        UpdateMemoryUsage();
    }

    private void UpdateMemoryUsage()
    {
        try
        {
            var memoryData = _logStorageService.GetCurrentMemoryUsage();
            MemoryBar.Value = memoryData.Item1;
            MemoryUsageTextBlock.Text = memoryData.Item2;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to Update Memory Usage: {e}");
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        StopMemoryUsageUpdate();
    }
}
