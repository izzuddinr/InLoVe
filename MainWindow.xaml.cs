using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InLoVe;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Frame> _pageFrames = new Dictionary<string, Frame>();

    public MainWindow()
    {
        InitializeComponent();
        InitializeFrames();
    }

    private void InitializeFrames()
    {
        _pageFrames["HomePage"] = CreateFrame(typeof(Pages.HomePage));
        _pageFrames["LogMonitoringPage"] = CreateFrame(typeof(Pages.LogMonitoringPage));
        _pageFrames["Iso8583ParsingPage"] = CreateFrame(typeof(Pages.Iso8583ParsingPage));

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
}