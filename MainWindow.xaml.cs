using System;
using System.Collections.Generic;
using InLoVe.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InLoVe;

public sealed partial class MainWindow : Window
{
    private readonly LogStorageService _logStorageService = App.Services.GetRequiredService<LogStorageService>();

    private readonly Dictionary<string, Frame> _pageFrames = new Dictionary<string, Frame>();

    public MainWindow()
    {
        InitializeComponent();
        InitializeFrames();
        StartMemoryUsageUpdate();
    }

    private void InitializeFrames()
    {
        _pageFrames["HomePage"] = CreateFrame(typeof(Pages.HomePage));
        _pageFrames["LogMonitoringPage"] = CreateFrame(typeof(Pages.LogMonitoringPage));
        _pageFrames["Iso8583ParsingPage"] = CreateFrame(typeof(Pages.Iso8583ParsingPage));
        _pageFrames["HostRecordPage"] = CreateFrame(typeof(Pages.HostRecordPage));

        ContentPresenter.Content = _pageFrames["HomePage"];
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

        if (_pageFrames.TryGetValue(tag, out var value))
        {
            ContentPresenter.Content = value;
        }
    }

    private void StartMemoryUsageUpdate()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (sender, e) => UpdateInfoBar();
        timer.Start();
    }

    private void UpdateInfoBar()
    {
        UpdateMemoryUsage();
        GetLogBufferSize();
    }

    private void GetLogBufferSize()
    {
        LogBufferSize.Text = _logStorageService.GetLogBufferSize();
    }

    private void UpdateMemoryUsage()
    {
        var memoryData = _logStorageService.GetCurrentMemoryUsage();
        MemoryBar.Value = memoryData.Item1;
        MemoryUsageTextBlock.Text = memoryData.Item2;
    }
}