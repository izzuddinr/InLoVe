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

using System.Diagnostics;
using Windows.UI;
using System.Drawing;
using Color = Windows.UI.Color;
using Microsoft.UI.Xaml.Media;
using ABI.Windows.UI;
using Microsoft.UI;
using Newtonsoft.Json;
namespace InLoVe.Pages;

public partial class HostRecordPage
{
    private readonly PubSubService? _pubSubService;
    private readonly ConfigService? _configService;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly List<string> _currentMessageBuffer = [];

    private bool _isParsingMessage;

    private string[] record = { "A Records", "X Records", "C Records" };

    private string[] AX_record = { "", "defaultAccount"};

    private string[] C_record = { "", "cardSaleEnable", "cardCashEnable", "cardVoidCashEnable", "enabled", "emvEnabled", "clessEnabled", "manualEnabled", "swipeEnabled",
                                    "txnAuthorityRequirement", "magstripePinRequired", "manualPinRequired", "cardPinBypassEnable", "cvcPrompt", "cvvBypassCheck", "checkSvc",
                                    "luhnCheckMode", "expDateCheckMode", "accountGroupingCodeOnline", "accountGroupingCodeOffline","addressVerification", "addressVerificationSwipe"};



    public HostRecordPage()
    {
        InitializeComponent();
        HostRecordScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
        OpenHostRecordFileButton.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());
        FindRecords.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configService = App.Services.GetService<ConfigService>();
        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);

        Records.ItemsSource = record;
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



    public static Dictionary<string, List<string>> ParseJsonFromFile()
    {
        try
        {
            // Read the entire content of the file as a string
            string json = File.ReadAllText(@"tags.json");

            // Deserialize the JSON string into a dictionary where keys are strings and values are lists of strings
            Dictionary<string, List<string>> parsedData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

            return parsedData;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading or parsing the file: " + ex.Message);
            return null;
        }
    }


    public void Records_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Records.SelectedItem == null)
        {
            RecordTags.ItemsSource = null;
            TagValue.ItemsSource = null;
            return;
        }

        if (Records.SelectedItem.ToString() == "A Records")
        {
            RecordTags.ItemsSource = AX_record;
        }

        if (Records.SelectedItem.ToString() == "X Records")
        {
            RecordTags.ItemsSource = AX_record;
        }

        if (Records.SelectedItem.ToString() == "C Records")
        {
            RecordTags.ItemsSource = C_record;
        }

    }

    public void RecordTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordTags.SelectedItem == null)
        {
            TagValue.ItemsSource = null;
            return;
        }

        var Tags = ParseJsonFromFile();

        // Check if the dictionary is not null
        if (Tags == null)
        {
            Console.WriteLine("Failed to parse JSON.");
            return;
        }

        // Get the selected key from RecordTags (assumed to be a string)
        string selectedTags = RecordTags.SelectedItem.ToString();

        // Check if the selected key exists in the dictionary
        if (Tags.ContainsKey(selectedTags))
        {
            // If the key is found, get the corresponding value (List<string>) and set it as the ItemsSource of TagValue
            TagValue.ItemsSource = Tags[selectedTags];
        }
        else
        {
            // If the key does not exist in the dictionary, clear the TagValue ItemsSource
            TagValue.ItemsSource = null;
            Console.WriteLine($"Key '{selectedTags}' not found in the dictionary.");
        }
    }

    public void FindRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        //Debug.WriteLine("Find Record now!");



    }


}