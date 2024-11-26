using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private static TreeView? _filteredTreeView;

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

        initTreeView();

        RecordTypeComboBox.ItemsSource = new List<string>{ "A Records", "X Records", "C Records", "K Records" };
    }

    private void initTreeView()
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

        _filteredTreeView = new TreeView
        {
            AllowDrop = false,
            CanDrag = false,
            CanDragItems = false,
            CanReorderItems = false,
            ItemTemplate = style
        };
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
            var hostRecordJson = HostRecordParser.ParseHostRecord(fileContents);

            AddMessageToTreeView(hostRecordJson);
            SwitchTreeView(false);
            Console.WriteLine($"_fullTreeView: {_unfilteredTreeView == null}");
            Console.WriteLine($"_fullTreeView.RootNodes.Count: {_unfilteredTreeView.RootNodes.Count}");
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
        var hostRecordJson = HostRecordParser.ParseHostRecord(_currentMessageBuffer);

        if (hostRecordJson == null) return;

        AddMessageToTreeView(hostRecordJson);
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private void AddMessageToTreeView(JObject hostRecordJson)
    {
        _unfilteredTreeView.RootNodes.Clear();

        foreach (var (key, value) in hostRecordJson)
        {
            var tag = key switch
            {
                "capkDataList" => HostRecordTag.K_RECORD,
                "hostAllCardConfigs" => HostRecordTag.CARD_CONFIGS,
                _ => HostRecordTag.UNKNOWN
            };
            var rootNode = new TreeViewNode
            {
                Content = new HostRecord
                {
                    Tag = tag.ToString(),
                    Value = $"{tag.GetHostRecordContent()}",
                    TextColor = ColorManager.GetBrush(AppColor.StartColor.ToString())
                }
            };
            Console.WriteLine($"rootNode - tag: {(rootNode.Content as HostRecord)?.Tag} value: {(rootNode.Content as HostRecord)?.Value}" );
            AddJsonElementToTreeView(rootNode, value);

            if (tag != HostRecordTag.K_RECORD) continue;
            _unfilteredTreeView.RootNodes.Add(rootNode);
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
                        var tag = key switch
                        {
                            "contactConfigs" => HostRecordTag.A_RECORD,
                            "clessConfig" => HostRecordTag.X_RECORD,
                            "cardConfigs" => HostRecordTag.C_RECORD,
                            _ => HostRecordTag.UNKNOWN
                        };

                        var childNode = new TreeViewNode
                        {
                            Content = new HostRecord
                            {
                                Tag = tag.ToString(),
                                Value = $"{tag.GetHostRecordContent()}",
                                TextColor = ColorManager.GetBrush(AppColor.StartColor.ToString())
                            }
                        };
                        Console.WriteLine($"childNode - tag: {(childNode.Content as HostRecord)?.Tag} value: {(childNode.Content as HostRecord)?.Value}" );
                        AddJsonElementToTreeView(childNode, nestedValue);
                        _unfilteredTreeView.RootNodes.Add(childNode);
                    }
                    else
                    {
                        if (key == "hostId") continue;
                        var childNode = new TreeViewNode
                        {
                            Content = new HostRecord
                            {
                                Tag = key,
                                Value = $"{key}: {nestedValue}",
                                TextColor = ColorManager.GetBrush(AppColor.WarningColor.ToString())
                            }
                        };
                        Console.WriteLine($"childNode - tag: {(childNode.Content as HostRecord)?.Tag} value: {(childNode.Content as HostRecord)?.Value}" );
                        parentNode.Children.Add(childNode);
                    }
                }

                break;

            case JArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var tag = (parentNode.Content as HostRecord)?.Tag;
                    var content = ExtractValueFromArray(array[i], tag);
                    var childNode = new TreeViewNode
                    {
                        Content = new HostRecord
                        {
                            Tag = tag,
                            Value = content ?? $"{i}",
                            TextColor = ColorManager.GetBrush(AppColor.ErrorColor.ToString())
                        }
                    };
                    Console.WriteLine($"childNode - tag: {(childNode.Content as HostRecord)?.Tag} value: {(childNode.Content as HostRecord)?.Value}" );
                    AddJsonElementToTreeView(childNode, array[i]);
                    parentNode.Children.Add(childNode);
                }

                break;

            default:
                var valueNode = new TreeViewNode
                {
                    Content = new HostRecord
                    {
                        Tag = $"{value}",
                        Value = $"{value}",
                        TextColor = ColorManager.GetBrush(AppColor.ErrorColor.ToString())
                    },
                };
                Console.WriteLine($"valueNode - tag: {(valueNode.Content as HostRecord)?.Tag} value: {(valueNode.Content as HostRecord)?.Value}" );
                parentNode.Children.Add(valueNode);
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
        _filteredTreeView.RootNodes.Clear();

        var unfilteredTree = _unfilteredTreeView;

        if (RecordTypeComboBox.SelectedItem == null || RecordTagComboBox.SelectedItem == null) return;

        var recordType = RecordTypeComboBox.SelectedItem?.ToString() switch {
            "A Records" => HostRecordTag.A_RECORD,
            "X Records" => HostRecordTag.X_RECORD,
            "C Records" => HostRecordTag.C_RECORD,
            "K Records" => HostRecordTag.K_RECORD,
            _ => HostRecordTag.UNKNOWN
        };

        var recordTag = RecordTagComboBox.SelectedItem?.ToString();

        var recordValue = RecordValueTextBox.Text;

        var rootNode = new TreeViewNode();

        foreach (var treeViewNode in unfilteredTree.RootNodes)
        {
            if (treeViewNode.Content is not HostRecord hostRecord || hostRecord.Tag.ToHostRecordTag() != recordType) continue;
            rootNode = new TreeViewNode
            {
                Content = new HostRecord
                {
                    Tag = $"{hostRecord.Tag}",
                    Value = $"{hostRecord.Value}",
                    TextColor = hostRecord.TextColor
                }
            };
        }

        _filteredTreeView.RootNodes.Add(rootNode);
        Console.WriteLine($"RootNodes: {_filteredTreeView.RootNodes.Count}");

        var uParentNode =
            unfilteredTree.RootNodes.Where(uRootNode =>
                uRootNode.Content is HostRecord hostRecord && hostRecord.Tag.ToHostRecordTag() == recordType
            ).ToList()[0];

        var uRecordNodes = uParentNode.Children.ToList();
        var fRecordNodes = new List<TreeViewNode>();
        foreach (var uRecordNode in uRecordNodes)
        {
            var fRecordNode = new TreeViewNode
            {
                Content = new HostRecord
                {
                    Tag = $"{(uRecordNode.Content as HostRecord)?.Tag}",
                    Value =$"{(uRecordNode.Content as HostRecord)?.Value}",
                    TextColor = ColorManager.GetBrush(AppColor.ErrorColor.ToString())
                },
            };

            var node = uRecordNode.Children
                .ToList()
                .Find(_node =>
                    _node.Content is HostRecord hostRecord && hostRecord.Tag == recordTag &&
                    hostRecord.Value.Contains(recordValue));


            if (node == null) continue;

            fRecordNode.Children.Add(node);
            fRecordNodes.Add(fRecordNode);
        }

        foreach (var root in _filteredTreeView.RootNodes)
        {
            root.Children.Clear();
        }

        fRecordNodes.ForEach(_filteredTreeView.RootNodes[0].Children.Add);
        SwitchTreeView(true);
    }

    private void ClearFilterRecordButton_OnClick(object sender, RoutedEventArgs e)
    {
        SwitchTreeView(false);
    }

    private void SwitchTreeView(bool showFiltered)
    {
        HostRecordContentControl.Content = null;
        HostRecordContentControl.Content = showFiltered ? _filteredTreeView : _unfilteredTreeView;
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