using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qatalyst.Controls;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;
using WinRT.Interop;

namespace Qatalyst.Pages;

public partial class Iso8583ParsingPage
{
    private readonly ConfigService? _configService;
    private readonly PubSubService? _pubSubService;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ConcurrentQueue<LogEntry> _logEntryQueue = new();
    private readonly List<string> _currentMessageBuffer = new();

    private List<TreeView> _isoMsgTreeView = [];
    private SolidColorBrush receiptTextColor;
    private bool _isParsingMessage;
    private CancellationTokenSource? _processingCancellationTokenSource;

    public Iso8583ParsingPage()
    {
        InitializeComponent();
        IsoMsgScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());
        ReceiptScrollViewer.Background = ColorManager.GetBrush(AppColor.VerboseColor.ToString());
        ExportIsoMsgLogButton.Background = ColorManager.GetBrush(AppColor.StopColor.ToString());
        receiptTextColor = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configService = App.Services.GetService<ConfigService>();
        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);

        _dispatcherQueue.TryEnqueue(
            () => AppendTextToView(
                [
                    "------------------------",
                    "         RECEIPT        ",
                    "------------------------",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                ]
            )
        );

        StartBackgroundProcessing();
    }

    private void StartBackgroundProcessing()
    {
        _processingCancellationTokenSource = new CancellationTokenSource();
        Task.Run(() => ProcessLogEntries(_processingCancellationTokenSource.Token));
    }

    private void StopBackgroundProcessing()
    {
        _processingCancellationTokenSource?.Cancel();
    }

    private void OnLogEntryReceived(object eventData)
    {
        if (eventData is LogEntry logEntry && !string.IsNullOrWhiteSpace(logEntry.Message))
        {
            _logEntryQueue.Enqueue(logEntry);
        }
    }

    private async Task ProcessLogEntries(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_logEntryQueue.TryDequeue(out var logEntry))
            {
                await ProcessLogEntryAsync(logEntry);
            }
            else
            {
                await Task.Delay(100, cancellationToken); // Avoid busy-waiting
            }
        }
    }

    private (bool IsValidTag, bool IsValidMessage, string MessageType) GetLogType(LogEntry logEntry)
    {
        if (_configService == null)
            return (false, false, string.Empty);

        var message = logEntry.Message;
        var tag = logEntry.Tag;


        // Determine message type
        var isReceiptTag = tag?.Contains("APP_PRINT") == true;
        var isIsoTag = _configService.Iso8583Filter?.Tag.Contains(tag) == true;
        var isTagValid = !string.IsNullOrEmpty(tag);
        var isMessageValid = isReceiptTag ?
            ContainsValidKeywords(message) :
            !string.IsNullOrEmpty(message) && message.Contains('|') && ContainsValidKeywords(message);

        var messageType = isReceiptTag ? "RECEIPT_MSG" : "ISO_MSG";

        // Final tag validity check
        var isValidTag = isTagValid && (isReceiptTag || isIsoTag);

        return (isValidTag, isMessageValid, messageType);
    }


    private static bool ContainsValidKeywords(string message) =>
        message.Contains("packRequest") ||
        message.Contains("unpackResponse") ||
        message.Contains("START PRINTING");


    private Task ProcessLogEntryAsync(LogEntry logEntry)
    {
        return Task.Run(() =>
        {
            var message = logEntry.Message;
            var (isValidTag, isValidMessage, messageType) = GetLogType(logEntry);

            if (ShouldFinalizeMessage(messageType, isValidTag, message)) FinalizeCurrentMessage(messageType);

            if (isValidTag && isValidMessage)
            {
                Console.WriteLine($"Tag: {logEntry.Tag} - Message: {message}");

                if (_isParsingMessage) FinalizeCurrentMessage(messageType);

                _isParsingMessage = true;
            }

            if (_isParsingMessage && isValidTag)
            {
                _currentMessageBuffer.Add(message);
            }
        });
    }

    private bool ShouldFinalizeMessage(string messageType, bool isValidTag, string message)
    {
        return messageType switch
        {
            "RECEIPT_MSG" => _isParsingMessage && isValidTag && message.Contains("END PRINTING"),
            "ISO_MSG" => _isParsingMessage && isValidTag && !message.Contains('|'),
            _ => false
        };
    }

    private void FinalizeCurrentMessage(string messageType = "ISO_MSG")
    {
        if (_currentMessageBuffer.Count == 0) return;

        var fullMessage = string.Join("\r\n", _currentMessageBuffer);

        Console.WriteLine($"fullMessage: {fullMessage}");

        switch (messageType)
        {
            case "RECEIPT_MSG":
                var receipt = ParseReceiptMessage();
                _dispatcherQueue.TryEnqueue(() => AppendTextToView(receipt));
                break;
            case "ISO_MSG":
                var isoMsg = Iso8583Parser.ParseIsoMessage(fullMessage);
                _dispatcherQueue.TryEnqueue(() => AddMessageToTreeView(isoMsg));
                break;
        }
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private List<string> ParseReceiptMessage()
    {
        var separator = Enumerable.Repeat(Environment.NewLine, 3).ToList();
        var formattedReceipt = new List<string>();
        var totalWidth = 20;

        foreach (var uLine in _currentMessageBuffer
                     .Where(line => !line.Contains("PrinterImpl"))
                     .Select(line => line.StartsWith("\t*") ? line.Substring(1) : line))
        {
            if (!uLine.Contains('\t') || uLine.Length >= totalWidth)
            {
                formattedReceipt.Add(uLine);
                continue;
            }

            var columns = uLine.Split("\t", StringSplitOptions.RemoveEmptyEntries);

            switch (columns.Length)
            {
                case 1:
                    formattedReceipt.Add(CenterAlign(columns[0], totalWidth));
                    break;

                case 2:
                    formattedReceipt.Add(AlignTwoColumns(columns[0], columns[1], totalWidth));
                    break;

                case 3:
                    formattedReceipt.Add(AlignThreeColumns(columns, totalWidth));
                    break;

                default:
                    formattedReceipt.Add(uLine);
                    break;
            }
        }

        return formattedReceipt.Concat(separator).ToList();
    }

    private static string CenterAlign(string text, int totalWidth)
    {
        var spaceCount = totalWidth - text.Length;
        if (spaceCount <= 0) return text;

        var leftPad = spaceCount / 2;
        var rightPad = spaceCount - leftPad;
        return $"{new string(' ', leftPad)}{text}{new string(' ', rightPad)}";
    }

    private static string AlignTwoColumns(string left, string right, int totalWidth)
    {
        var spaceCount = totalWidth - (left.Length + right.Length);
        if (spaceCount < 0) spaceCount = 0; // Prevent negative spaces
        return $"{left}{new string(' ', spaceCount)}{right}";
    }

    private static string AlignThreeColumns(string[] columns, int totalWidth)
    {
        if (columns.Length != 3) return string.Join(" ", columns);

        var usedWidth = columns[0].Length + columns[1].Length + columns[2].Length;
        var spaceCount = totalWidth - usedWidth;
        if (spaceCount < 0) spaceCount = 0; // Prevent negative spaces

        var leftSpace = spaceCount / 2;
        var rightSpace = spaceCount - leftSpace;

        return $"{columns[0]}{new string(' ', leftSpace)}{columns[1]}{new string(' ', rightSpace)}{columns[2]}";
    }


    private void AddMessageToTreeView(ISO8583Msg isoMsg)
    {
        // Create a new TreeView for this ISO8583 message
        var newTreeView = new TreeView
        {
            Margin = new Thickness(5),
            AllowDrop = false,
            CanDragItems = false,
            CanReorderItems = false,
            CanDrag = false,
            ItemTemplate = Application.Current.Resources["HostRecordTreeViewNodeTemplate"] as DataTemplate
        };

        var color = isoMsg.IsRequestMsg()
            ? ColorManager.GetBrush(AppColor.InfoColor.ToString())
            : ColorManager.GetBrush(AppColor.WarningColor.ToString());

        // Create the root node using the MTI
        var rootNode = new TreeViewNode
        {
            Content = new CustomTreeViewContent
            {
                Tag = "MTI",
                Value = $"MTI: {isoMsg.MessageType}",
                TextColor = color
            }
        };

        // Add fields to the root node
        foreach (var data in isoMsg.DataElements)
        {
            var subfieldNode = new TreeViewNode();
            var length = data.Value.Length?.ToString().PadLeft(4, '0');
            var values = data.Value.Value;

            if (data.Value.Length == null && data.Value.Value.Count == 1)
            {
                subfieldNode.Content = CreateNewNode(data, color);
                rootNode.Children.Add(subfieldNode);
                continue;
            }

            if (data.Key == 55)
            {
                rootNode.Children.Add(BuildEmvDataTree(data.Key, data.Value, color));
                continue;
            };

            subfieldNode.Content = CreateNewNode(data, color, length);

            foreach (var value in values)
            {
                subfieldNode.Children.Add(new TreeViewNode { Content = CreateNewNode(value, color) });
            }

            rootNode.Children.Add(subfieldNode);
        }

        // Add the root node to the TreeView
        newTreeView.RootNodes.Add(rootNode);

        _isoMsgTreeView.Add(newTreeView);

        // Add the TreeView dynamically to the ScrollViewer
        if (IsoMsgScrollViewer.Content is StackPanel stackPanel)
        {
            stackPanel.Children.Add(newTreeView);
        }
        else
        {
            var newStackPanel = new StackPanel();
            newStackPanel.Children.Add(newTreeView);
            IsoMsgScrollViewer.Content = newStackPanel;
        }
    }

    private TreeViewNode BuildEmvDataTree(int key, ISO8583DataElement data, Brush color)
    {
        var subfieldNode = new TreeViewNode
        {
            Content = CreateNewEmvNode(key, data.Length, color)
        };

        var emvData = string.Join(string.Empty, data.Value);
        subfieldNode.Children.Add(new TreeViewNode { Content = CreateNewNode(FormatRawEmvData(emvData), color)});

        var index = 0;
        while (index < emvData.Length)
        {
            var tag = emvData.Substring(index, 2);
            index += 2;

            if ((Convert.ToByte(tag, 16) & 0x1F) == 0x1F)
            {
                tag += emvData.Substring(index, 2);
                index += 2;
            }

            var length = Convert.ToInt32(emvData.Substring(index, 2), 16);
            index += 2;

            var value = emvData.Substring(index, length * 2);
            index += length * 2;

            var childNode = new TreeViewNode
            {
                Content = CreateNewEmvNode(tag, length, value, color)
            };
            subfieldNode.Children.Add(childNode);
        }
        return subfieldNode;
    }

    private static string FormatRawEmvData(string rawEmvData)
    {
        const int lineLength = 80;
        var formattedData = new StringBuilder();

        for (var i = 0; i < rawEmvData.Length; i += lineLength)
        {
            var length = Math.Min(lineLength, rawEmvData.Length - i);
            formattedData.AppendLine(rawEmvData.Substring(i, length));
        }

        return formattedData.ToString().TrimEnd();
    }


    private static CustomTreeViewContent CreateNewNode(string value, Brush color) => new() { Value = value, TextColor = color};


    private static CustomTreeViewContent CreateNewNode(KeyValuePair<int,ISO8583DataElement> data, Brush color, string? length = null)
    {
        var values = data.Value.Value;

        var value = length != null
            ? $"{data.Key.ToString().PadLeft(3, '0')} : ({length})"
            : $"{data.Key.ToString().PadLeft(3, '0')} : {values[0]}";

        return new CustomTreeViewContent
        {
            Tag = $"{data.Key.ToString().PadLeft(3, '0')}",
            Value = value,
            TextColor = color
        };
    }

    private static CustomTreeViewContent CreateNewEmvNode(int key, int? length, Brush color)
    {
        var value = $"{key.ToString().PadLeft(3, '0')} : ({length})";

        return new CustomTreeViewContent
        {
            Tag = "EMV DATA",
            Value = value,
            TextColor = color
        };
    }

    private static CustomTreeViewContent CreateNewEmvNode(string? tag, int length, string? value, Brush color)
    {
        return new CustomTreeViewContent
        {
            Tag = $"{tag,-8}",
            Value = $"{tag,-8}: ({length}) {value}",
            TextColor = color
        };
    }

    private async void ExportIsoMsgLogButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var suggestedFileName = "OUTPUT_ISO8583_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json";
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName,
                DefaultFileExtension = ".json",
            };

            // Add file types to save picker
            savePicker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });

            var window = App.MainAppWindow;

            var hWnd = WindowNative.GetWindowHandle(window);

            InitializeWithWindow.Initialize(savePicker, hWnd);

            var file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                await ExportIsoMsg(file.Path);
                Console.WriteLine("File saved successfully.");
            }
            else
            {
                Console.WriteLine("Save operation was cancelled.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving file: " + ex.Message);
        }
    }

    private async Task ExportIsoMsg(string fileName)
    {
        if (_isParsingMessage) return;

        try
        {
            var jsonObj = new JObject();

            // Extract data from the TreeView nodes
            foreach (var rootNode in _isoMsgTreeView.Select(view => view.RootNodes.FirstOrDefault()))
            {
                if (rootNode == null || rootNode.Content is not CustomTreeViewContent rootNodeContent) continue;
                ProcessChildNodeToJson(jsonObj, rootNodeContent.Value, rootNode);
            }

            await ExportToJsonFileAsync(jsonObj, fileName);

            Console.WriteLine($"JSON exported successfully to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting JSON: {ex.Message}");
        }
    }

    private void ProcessChildNodeToJson(JObject parentJson, string key, TreeViewNode node)
    {
        var jArray = new JArray();

        foreach (var childNode in node.Children)
        {
            if (childNode?.Content is not CustomTreeViewContent childNodeContent)
                continue;

            if (!childNode.HasChildren)
            {
                jArray.Add(childNodeContent.Value);
            }
            else if (childNode.Children.Count == 1)
            {
                if (childNode.Children.FirstOrDefault()?.Content is not CustomTreeViewContent subChildContent) continue;
                jArray.Add($"{childNodeContent.Value} {subChildContent.Value}");
            }
            else
            {
                // Recursively process child nodes and collect their values
                var childValues = childNode.Children
                    .Where(x => x.Content is CustomTreeViewContent)
                    .Select(x => ((CustomTreeViewContent)x.Content).Value);

                jArray.Add(new JArray(childValues));
            }
        }
        parentJson[key] = jArray;
    }

    public static async Task ExportToJsonFileAsync(JObject jsonObject, string filePath)
    {
        Formatting formatting = Formatting.Indented;
        try
        {
            await using var writer = new StreamWriter(filePath);
            await using var jsonWriter = new JsonTextWriter(writer) { Formatting = formatting };
            await jsonObject.WriteToAsync(jsonWriter);
        }
        catch (Exception ex)
        {
            throw new IOException("Failed to export JSON.", ex);
        }
    }


    private void AppendTextToView(List<string> input)
    {
        var paragraph = ReceiptTextBlock.Blocks[0] as Paragraph;
        if (paragraph == null) return;

        var lines = input.Select(
            item => new Run
            {
                Text = item + Environment.NewLine,
                Foreground = receiptTextColor
            });

        foreach (var line in lines)
        {
            paragraph.Inlines.Add(line);
        }
    }

}