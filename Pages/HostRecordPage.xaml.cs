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
using Qatalyst.Controls;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public partial class HostRecordPage
{
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
        HostRecordScrollViewer.Background = ColorManager.GetBrush("AppBackgroundColor");
        OpenHostRecordFileButton.Background = ColorManager.GetBrush("StartColor");
        FindRecordButton.Background = ColorManager.GetBrush("StartColor");
        ClearFilterRecordButton.Background = ColorManager.GetBrush("StopColor");
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configService = App.Services.GetService<ConfigService>();
        var pubSubService = App.Services.GetService<PubSubService>();
        pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);

        _normalColorBrush = ColorManager.GetBrush("VerboseColor");
        _filteredColorBrush = ColorManager.GetBrush("StopColor");

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
            _currentActiveJson = HostRecordParser.ParseHostRecord(fileContents) ?? new JObject();

            AddMessageToTreeView(_currentActiveJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading file: " + ex.Message);
        }
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
        var isHostRecordLogStart = message != null && message.Contains("capkDataList");

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
                if (message != null) _currentMessageBuffer.Add(message);
                break;
        }
    }

    private void FinalizeCurrentMessage()
    {
        _currentActiveJson = HostRecordParser.ParseHostRecord(_currentMessageBuffer) ?? new JObject();

        AddMessageToTreeView(_currentActiveJson);
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private void AddMessageToTreeView(JObject hostRecordJson, bool isFilteredView = false)
    {
        if (_unfilteredTreeView == null) return;

        _unfilteredTreeView.RootNodes.Clear();
        Console.WriteLine($"Cleared unfiltered treeview");

        foreach (var (key, value) in hostRecordJson)
        {
            var recordTypeFilter = RecordTypeComboBox.SelectedItem?.ToString() switch
            {
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
                Content = new CustomTreeViewContent
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
            if (value != null) AddJsonElementToTreeView(rootNode, value, isFilteredView);

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
                        var isTagMatchFilter = isFilteredView && tag == recordTypeFilter;

                        var childNode = new TreeViewNode
                        {
                            Content = new CustomTreeViewContent
                            {
                                Tag = tag.ToString(),
                                Value = $"{tag.GetHostRecordContent()}",
                                TextColor = isFilteredView && isTagMatchFilter ? _filteredColorBrush : _normalColorBrush
                            }
                        };

                        AddJsonElementToTreeView(childNode, nestedValue, isFilteredView);
                        if (_unfilteredTreeView != null) _unfilteredTreeView.RootNodes.Add(childNode);
                    }
                    else
                    {
                        if (key == "hostId") continue;
                        var isTagMatchFilter = isFilteredView && key == recordTagFilter;
                        var isValueMatchFilter = isFilteredView &&
                                                 (RecordValueTextBox.Text.IsNullOrWhiteSpace() ||
                                                 value.ToString().Contains(recordValueFilter.Trim()));
                        
                        var childNode = new TreeViewNode
                        {
                            Content = new CustomTreeViewContent
                            {
                                Tag = key,
                                Value = $"{key}: {nestedValue}",
                                TextColor = isFilteredView && isTagMatchFilter && isValueMatchFilter ? _filteredColorBrush : _normalColorBrush
                            }
                        };
                        parentNode.Children.Add(childNode);
                    }
                }

                break;

            case JArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var tag = (parentNode.Content as CustomTreeViewContent)?.Tag;

                    if (tag == null) continue;

                    var content = ExtractValueFromArray(array[i], tag);
                    var isTypeMatchFilter = isFilteredView && tag == recordTypeFilter.ToString();

                    var isValueMatchFilter = false;
                    if (isFilteredView)
                    {
                        foreach (var element in (JObject)array[i])
                        {
                            if (element.Key != recordTagFilter) continue;
                            var foundMatchingRecord = element.Value != null && element.Value.ToString().Contains(recordValueFilter.Trim());

                            if (!foundMatchingRecord) continue;
                            isValueMatchFilter = foundMatchingRecord;
                            break;
                        }
                    }

                    var childNode = new TreeViewNode
                    {
                        Content = new CustomTreeViewContent
                        {
                            Tag = tag,
                            Value = content ?? $"{i}",
                            TextColor = isFilteredView && isTypeMatchFilter && isValueMatchFilter ? _filteredColorBrush : _normalColorBrush
                        }
                    };

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
                    GetEmvRecordsValue(arrayItem, "aid", "recommendedAppName"),

                HostRecordTag.C_RECORD =>
                    GetCardConfigsValue(arrayItem, "binRangeLow", "binRangeHigh", "cardScheme"),

                HostRecordTag.K_RECORD =>
                    GetCapkValue(arrayItem, "rid", "ridIndex", "exponent"),

                _ => null
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private string FormatCapkLabel(string label, string inputKey) => $"{label} [{inputKey}]";

    private string GetCapkValue(JToken arrayItem, string rid, string ridIndex, string exponent)
    {
        if (arrayItem is not JObject capkObject ||
            !capkObject.TryGetValue(rid, out var ridValue) ||
            !capkObject.TryGetValue(ridIndex, out var indexValue) ||
            !capkObject.TryGetValue(exponent, out var exponentValue)) return string.Empty;

        var inputKey = $"{ridValue}{indexValue}{exponentValue}";
        var label = _configService != null
                ? _configService.CapkLabels.FirstOrDefault(cl => cl.Key == inputKey)?.Label ?? "OTHERS"
                : "OTHERS";
        return FormatCapkLabel(label, $"{ridValue} {indexValue} {exponentValue}");
    }

    private string? GetCardConfigsValue(JToken arrayItem, string binRangeLow, string binRangeHigh, string cardScheme)
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

    private string? GetEmvRecordsValue(JToken arrayItem, string aid, string appName)
    {
        if (arrayItem is JObject emvObject &&
            emvObject.TryGetValue(aid, out var aidValue) &&
            emvObject.TryGetValue(appName, out var appNameValue))
            return $"{aidValue} ({appNameValue.ToString().Trim()})";
        return null;
    }

    private void RecordTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordTypeComboBox.SelectedItem == null) return;

        RecordTagComboBox.ItemsSource = null;
        RecordTagComboBox.ItemsSource = RecordTypeComboBox.SelectedItem.ToString() switch
        {
            "A Records" or "X Records" => HostRecordTag.A_RECORD.GetRecordTags(),
            "C Records" => HostRecordTag.C_RECORD.GetRecordTags(),
            "K Records" => HostRecordTag.K_RECORD.GetRecordTags(),
            _ => RecordTagComboBox.ItemsSource
        };
    }

    private void RecordValueTextBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        RecordValueTextBoxGetAutoSuggestion(sender);
    }

    private void RecordValueTextBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if(sender is not AutoSuggestBox autoSuggestBox) return;

        RecordValueTextBoxGetAutoSuggestion(autoSuggestBox);
    }

    private void RecordValueTextBoxGetAutoSuggestion(AutoSuggestBox sender)
    {
        if (_configService == null || RecordTagComboBox.SelectedItem == null) return;

        _configService.HostRecordTags.TryGetValue(
            RecordTagComboBox.SelectedItem?.ToString() ?? "", out var suitableItems
        );

        var splitText = sender.Text.ToLower().Split(" ");
        if (suitableItems == null) return;

        foreach (var item in from item in suitableItems.ToList()
                 let found = splitText.All(key => item.Contains(key, StringComparison.CurrentCultureIgnoreCase))
                 where found
                 select item)
        {
            if (suitableItems.Contains(item)) continue;
            suitableItems.Add(item);
        }

        if (suitableItems.Count == 0)
        {
            suitableItems.Add("No results found");
        }

        sender.ItemsSource = suitableItems;
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
            Console.WriteLine($"{(root.Content as CustomTreeViewContent)?.Value}");
            foreach (var x in root.Children)
            {
                Console.WriteLine($"->{(x.Content as CustomTreeViewContent)?.Value}");
                foreach (var y in x.Children)
                {
                    Console.WriteLine($"-->{(y.Content as CustomTreeViewContent)?.Value}");
                    foreach (var z in y.Children)
                    {
                        Console.WriteLine($"--->{(z.Content as CustomTreeViewContent)?.Value}");
                    }

                }
            }
        }
        Console.WriteLine("=================================================");
    }
}