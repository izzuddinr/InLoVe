using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using WinRT.Interop;
using Newtonsoft.Json;
using Qatalyst.Controls;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public partial class HostRecordPage
{
    private readonly PubSubService? _pubSubService;
    private readonly ConfigService? _configService;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly List<string> _currentMessageBuffer = [];

    private bool _isParsingMessage;
    private static TreeView? _unfilteredTreeView;

    private static JObject _currentActiveJson = new();

    private readonly SolidColorBrush _normalColorBrush;
    private readonly SolidColorBrush _filteredColorBrush;

    public HostRecordPage()
    {
        InitializeComponent();
        HostRecordScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
        OpenHostRecordFileButton.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());
        FindRecordButton.Background = ColorManager.GetBrush(AppColor.StartColor.ToString());
        ClearFilterRecordButton.Background = ColorManager.GetBrush(AppColor.StopColor.ToString());
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configService = App.Services.GetService<ConfigService>();
        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);

        _normalColorBrush = ColorManager.GetBrush(AppColor.VerboseColor.ToString());
        _filteredColorBrush = ColorManager.GetBrush(AppColor.StopColor.ToString());

        InitTreeView();

        RecordTypeComboBox.ItemsSource = new List<string>{ "A Records", "X Records", "C Records", "K Records" };
    }

    private void InitTreeView()
    {
        var style = Application.Current.Resources["HostRecordTreeViewNodeTemplate"] as DataTemplate;
        _unfilteredTreeView = new TreeView
        {
            AllowDrop = false,
            CanDrag = false,
            CanDragItems = false,
            CanReorderItems = false,
            ItemTemplate = style
        };

        HostRecordContentControl.Content = _unfilteredTreeView;
    }

    private async void OpenHostRecordFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker
            {
                FileTypeFilter = { ".txt" },
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List,
            };
            var window = App.MainAppWindow;

            var hWnd = WindowNative.GetWindowHandle(window);

            InitializeWithWindow.Initialize(openPicker, hWnd);

            var file = await openPicker.PickSingleFileAsync();
            var fileContents = await File.ReadAllTextAsync(file.Path);
            _currentActiveJson = HostRecordParser.ParseHostRecord(fileContents);

            AddMessageToTreeView(_currentActiveJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading file: " + ex.Message);
        }
    }

    private async void SaveHostRecordFileAsXmlButton_OnClick(object sender, RoutedEventArgs e)
    {
        return;
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
        _currentActiveJson = HostRecordParser.ParseHostRecord(_currentMessageBuffer);

        if (_currentActiveJson == null) return;

        AddMessageToTreeView(_currentActiveJson);
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private void AddMessageToTreeView(JObject hostRecordJson, bool isFilteredView = false)
    {
        _unfilteredTreeView.RootNodes.Clear();
        Console.WriteLine($"Cleared unfiltered treeview");

        foreach (var (key, value) in hostRecordJson)
        {
            var recordTypeFilter = RecordTypeComboBox.SelectedItem?.ToString() switch {
                "A Records" => HostRecordTag.A_RECORD,
                "X Records" => HostRecordTag.X_RECORD,
                "C Records" => HostRecordTag.C_RECORD,
                "K Records" => HostRecordTag.K_RECORD,
                _ => HostRecordTag.UNKNOWN
            };

            var tag = key switch
            {
                "capkDataList" => HostRecordTag.K_RECORD,
                "hostAllCardConfigs" => HostRecordTag.CARD_CONFIGS,
                _ => HostRecordTag.UNKNOWN
            };

            var isTypeMatchFilter = isFilteredView && tag == recordTypeFilter;

            var rootNode = new TreeViewNode
            {
                Content = new HostRecord
                {
                    Tag = tag.ToString(),
                    Value = $"{tag.GetHostRecordContent()}",
                    TextColor = isTypeMatchFilter switch
                    {
                        true => _filteredColorBrush,
                        false => _normalColorBrush
                    }
                }
            };
            //Console.WriteLine($"rootNode - tag: {(rootNode.Content as HostRecord)?.Tag} value: {(rootNode.Content as HostRecord)?.Value}" );
            AddJsonElementToTreeView(rootNode, value, isFilteredView);

            if (tag != HostRecordTag.K_RECORD) continue;
            _unfilteredTreeView.RootNodes.Add(rootNode);
        }
    }

    private void AddJsonElementToTreeView(TreeViewNode parentNode, JToken value, bool isFilteredView)
    {
        var recordTypeFilter = RecordTypeComboBox.SelectedItem?.ToString() switch {
            "A Records" => HostRecordTag.A_RECORD,
            "X Records" => HostRecordTag.X_RECORD,
            "C Records" => HostRecordTag.C_RECORD,
            "K Records" => HostRecordTag.K_RECORD,
            _ => HostRecordTag.UNKNOWN
        };

        var recordTagFilter = RecordTagComboBox.SelectedItem?.ToString();

        var recordValueFilter = RecordValueTextBox.Text;

        switch (value)
        {
            case JObject nestedObject:
                foreach (var (key, nestedValue) in nestedObject)
                {
                    if (nestedValue is JArray)
                    {
                        var tag = key switch
                        {
                            "contactConfigs" => HostRecordTag.A_RECORD,
                            "clessConfig" => HostRecordTag.X_RECORD,
                            "cardConfigs" => HostRecordTag.C_RECORD,
                            _ => HostRecordTag.UNKNOWN
                        };

                        var isTypeMatchFilter = isFilteredView && tag == recordTypeFilter;

                        var childNode = new TreeViewNode
                        {
                            Content = new HostRecord
                            {
                                Tag = tag.ToString(),
                                Value = $"{tag.GetHostRecordContent()}",
                                TextColor = isFilteredView && isTypeMatchFilter ? _filteredColorBrush : _normalColorBrush
                            }
                        };

                        //Console.WriteLine($"childNestedNode - tag: {(childNode.Content as HostRecord)?.Tag} value: {(childNode.Content as HostRecord)?.Value}" );
                        AddJsonElementToTreeView(childNode, nestedValue, isFilteredView);
                        _unfilteredTreeView.RootNodes.Add(childNode);
                    }
                    else
                    {
                        if (key == "hostId") continue;
                        var isTagMatchFilter = isFilteredView && key == recordTagFilter;
                        var isValueMatchFilter = isFilteredView &&
                                                 (RecordValueTextBox.Text.IsNullOrWhiteSpace() ||
                                                 value.Contains(recordValueFilter ?? string.Empty));

                        // Console.WriteLine($"childNode Filter - tag: {key} filter: {recordTagFilter}" );
                        var childNode = new TreeViewNode
                        {
                            Content = new HostRecord
                            {
                                Tag = key,
                                Value = $"{key}: {nestedValue}",
                                TextColor = isFilteredView && isTagMatchFilter && isValueMatchFilter ? _filteredColorBrush : _normalColorBrush
                            }
                        };

                        // Console.WriteLine($"childNode - tag: {(childNode.Content as HostRecord)?.Tag} value: {(childNode.Content as HostRecord)?.Value}" );
                        parentNode.Children.Add(childNode);
                    }
                }

                break;

            case JArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var tag = (parentNode.Content as HostRecord)?.Tag;
                    var content = ExtractValueFromArray(array[i], tag);
                    var isTypeMatchFilter = isFilteredView && tag == recordTypeFilter.ToString();

                    var childNode = new TreeViewNode
                    {
                        Content = new HostRecord
                        {
                            Tag = tag,
                            Value = content ?? $"{i}",
                            TextColor = isFilteredView && isTypeMatchFilter ? _filteredColorBrush : _normalColorBrush
                        }
                    };

                    // Console.WriteLine($"childArrayNode - tag: {(childNode.Content as HostRecord)?.Tag} value: {(childNode.Content as HostRecord)?.Value}" );
                    AddJsonElementToTreeView(childNode, array[i], isFilteredView);
                    parentNode.Children.Add(childNode);
                }

                break;
        }
    }

    private string? ExtractValueFromArray(JToken arrayItem, string tag)
    {
        try
        {
            var tagSwitch = tag.ToHostRecordTag();
            return tagSwitch switch
            {
                HostRecordTag.A_RECORD or HostRecordTag.X_RECORD =>
                    getEmvRecordsValue(arrayItem, "aid", "recommendedAppName"),

                HostRecordTag.C_RECORD =>
                    getCardConfigsValue(arrayItem, "binRangeLow", "binRangeHigh", "cardScheme"),

                HostRecordTag.K_RECORD =>
                    getCapkValue(arrayItem, "rid", "ridIndex", "exponent"),

                _ => null
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private string? getCapkValue(JToken arrayItem, string rid, string ridIndex, string exponent)
    {
        if (arrayItem is not JObject capkObject ||
            !capkObject.TryGetValue(rid, out var ridValue) ||
            !capkObject.TryGetValue(ridIndex, out var indexValue) ||
            !capkObject.TryGetValue(exponent, out var exponentValue)) return null;

        return _configService.CapkDictionary.TryGetValue($"{ridValue}{indexValue}{exponentValue}", out var capkLabel)
            ? $"{capkLabel} ({ridValue}, {indexValue}, {exponentValue})"
            : $"OTHERS ({ridValue}, {indexValue}, {exponentValue})";
    }

    private string? getCardConfigsValue(JToken arrayItem, string binRangeLow, string binRangeHigh, string cardScheme)
    {
        if (arrayItem is JObject binRangeObject &&
            binRangeObject.TryGetValue(binRangeLow, out var lowValue) &&
            binRangeObject.TryGetValue(binRangeHigh, out var highValue) &&
            binRangeObject.TryGetValue(cardScheme, out var cardSchemeValue))
        {
            return $"{lowValue.ToString().PadRight(8, '0')}-{highValue} ({cardSchemeValue})";
        }

        return null;
    }

    private string? getEmvRecordsValue(JToken arrayItem, string aid, string appName)
    {
        if (arrayItem is JObject emvObject &&
            emvObject.TryGetValue(aid, out var aidValue) &&
            emvObject.TryGetValue(appName, out var appNameValue))
            return $"{aidValue} ({appNameValue.ToString().Trim()})";
        return null;
    }

    public void Records_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordTypeComboBox.SelectedItem == null)
        {
            return;
        }

        RecordTagComboBox.ItemsSource = null;
        RecordTagComboBox.ItemsSource = RecordTypeComboBox.SelectedItem.ToString() switch
        {
            "A Records" or "X Records" => HostRecordTag.A_RECORD.GetRecordTags(),
            "C Records" => HostRecordTag.C_RECORD.GetRecordTags(),
            "K Records" => HostRecordTag.K_RECORD.GetRecordTags(),
            _ => RecordTagComboBox.ItemsSource
        };
    }

    public void FindRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (RecordTypeComboBox.SelectedIndex > -1 && RecordTagComboBox.SelectedIndex > -1)
            AddMessageToTreeView(_currentActiveJson, true);
    }

    private void ClearFilterRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        RecordTypeComboBox.SelectedIndex = -1;
        RecordTagComboBox.SelectedIndex = -1;
        RecordTypeComboBox.Text = string.Empty;

        AddMessageToTreeView(_currentActiveJson);
    }

    private void PrintToConsole(TreeView treeView)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("Unfiltered");
        Console.WriteLine("=================================================");
        foreach (var root in treeView.RootNodes)
        {
            Console.WriteLine($"{(root.Content as HostRecord)?.Value}");
            foreach (var x in root.Children)
            {
                Console.WriteLine($"->{(x.Content as HostRecord)?.Value}");
                foreach (var y in x.Children)
                {
                    Console.WriteLine($"-->{(y.Content as HostRecord)?.Value}");
                    foreach (var z in y.Children)
                    {
                        Console.WriteLine($"--->{(z.Content as HostRecord)?.Value}");
                    }

                }
            }
        }
        Console.WriteLine("=================================================");
    }
 }