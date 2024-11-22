using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;
using InLoVe.Objects;
using InLoVe.Services;
using InLoVe.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using WinRT.Interop;


namespace InLoVe.Pages;

public partial class HostRecordPage
{
    private readonly PubSubService? _pubSubService;
    private readonly ConfigService? _configService;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly List<string> _currentMessageBuffer = [];

    private bool _isParsingMessage;

    public HostRecordPage()
    {
        InitializeComponent();
        HostRecordScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
        OpenHostRecordFileButton.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configService = App.Services.GetService<ConfigService>();
        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);
    }

    private void OnLogEntryReceived(object eventData)
    {
        if (eventData is not LogEntry logEntry || string.IsNullOrWhiteSpace(logEntry.Message))
            return;

        _dispatcherQueue.TryEnqueue(() => { ProcessLogEntry(logEntry); });
    }

    private void ProcessLogEntry(LogEntry logEntry)
    {
        var message = logEntry.Message;
        var isValidPackageName = logEntry.PackageName != null && logEntry.PackageName.Contains("haas.paymark");
        var isValidTag = logEntry.Tag != null && logEntry.Tag.Contains("APP_CMD_PROXY");
        var isHostRecordLogStart = message.Contains("capkDataList");

        if (_isParsingMessage && !isValidPackageName && !isValidTag) FinalizeCurrentMessage();

        if (isValidPackageName && isValidTag && isHostRecordLogStart)
        {
            if (_isParsingMessage) FinalizeCurrentMessage();

            _isParsingMessage = true;
        }

        switch (_isParsingMessage)
        {
            case false:
                return;
            case true when isValidTag:
                _currentMessageBuffer.Add(message);
                FinalizeCurrentMessage();
                break;
        }
    }

    private void FinalizeCurrentMessage()
    {
        var hostRecordJson = HostRecordParser.ParseHostRecord(_currentMessageBuffer);

        if (hostRecordJson == null) return;

        AddMessageToTreeView(hostRecordJson);
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private async void OpenHostRecordFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();
            var window = App.MainAppWindow;

            var hWnd = WindowNative.GetWindowHandle(window);

            InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".txt");
            var file = await openPicker.PickSingleFileAsync();

            var filePath = @"F:\_Works_\TestHostRecordString.txt";
            var fileContents = await File.ReadAllTextAsync(file.Path);
            var hostRecordJson = HostRecordParser.ParseHostRecord(fileContents);

            AddMessageToTreeView(hostRecordJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading file: " + ex.Message);
        }
    }

    private void AddMessageToTreeView(JObject hostRecordJson)
    {
        HostRecordTreeView.RootNodes.Clear();

        foreach (var (key, value) in hostRecordJson)
        {
            var rootNode = new TreeViewNode { Content = key };
            AddJsonElementToTreeView(rootNode, value);
            HostRecordTreeView.RootNodes.Add(rootNode);
        }
    }

    private void AddJsonElementToTreeView(TreeViewNode parentNode, JToken value)
    {
        switch (value)
        {
            case JObject nestedObject:
                foreach (var (key, nestedValue) in nestedObject)
                {
                    if (nestedValue is JArray)
                    {
                        var childNode = new TreeViewNode { Content = $"{key}" };
                        AddJsonElementToTreeView(childNode, nestedValue);
                        parentNode.Children.Add(childNode);
                    }
                    else
                    {
                        var childNode = new TreeViewNode { Content = $"{key}: {nestedValue}" };
                        parentNode.Children.Add(childNode);
                    }
                }

                break;

            case JArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var content = string.Empty;

                    switch (parentNode.Content)
                    {
                        case "capkDataList":
                            content = ExtractValueFromArray(array[i], "rid", "ridIndex", "exponent");
                            break;
                        case "contactConfigs":
                        case "clessConfig":
                            content = ExtractValueFromArray(array[i], "aid");
                            break;
                        case "cardConfigs":
                            content = ExtractValueFromArray(array[i], "binRangeLow", "binRangeHigh");
                            break;
                        default:
                            content = $"[{i}]";
                            break;
                    }

                    var childNode = new TreeViewNode { Content = content ?? $"{i}" };
                    AddJsonElementToTreeView(childNode, array[i]);
                    parentNode.Children.Add(childNode);
                }

                break;

            default:
                var valueNode = new TreeViewNode { Content = value?.ToString() ?? "null" };
                parentNode.Children.Add(valueNode);
                break;
        }
    }

    private string? ExtractValueFromArray(JToken arrayItem, params string[] keys)
    {
        try
        {
            switch (keys.Length)
            {
                case 1:
                    if (arrayItem is JObject emvObject && emvObject.TryGetValue(keys[0], out var value))
                    {
                        return $"{value}";
                    }
                    return null;
                case 2:
                    if (arrayItem is JObject binRangeObject &&
                        binRangeObject.TryGetValue(keys[0], out var lowValue) &&
                        binRangeObject.TryGetValue(keys[1], out var highValue))
                    {
                        return $"{lowValue.ToString().PadRight(8, '0')}_{highValue}";
                    }
                    return null;
                case 3:
                    if (arrayItem is not JObject capkObject ||
                        !capkObject.TryGetValue(keys[0], out var rid) ||
                        !capkObject.TryGetValue(keys[1], out var index) ||
                        !capkObject.TryGetValue(keys[2], out var exponent)) return null;

                    return _configService.CapkDictionary.TryGetValue($"{rid}{index}{exponent}", out var capkLabel)
                        ? $"{capkLabel} ({rid}, {index}, {exponent})"
                        : $"UNKNOWN ({rid}, {index}, {exponent})";
                default:
                    return null;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
}