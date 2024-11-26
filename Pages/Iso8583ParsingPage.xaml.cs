using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Qatalyst.Controls;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public partial class Iso8583ParsingPage
{
    private readonly ConfigService? _configService;
    private readonly PubSubService? _pubSubService;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ConcurrentQueue<LogEntry> _logEntryQueue = new();
    private readonly List<string> _currentMessageBuffer = new();

    private bool _isParsingMessage;
    private CancellationTokenSource? _processingCancellationTokenSource;

    public Iso8583ParsingPage()
    {
        InitializeComponent();
        IsoMsgScrollViewer.Background = ColorManager.GetBrush(AppColor.AppBackgroundColor.ToString());

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _configService = App.Services.GetService<ConfigService>();
        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);

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

    private Task ProcessLogEntryAsync(LogEntry logEntry)
    {
        return Task.Run(() =>
        {
            var message = logEntry.Message;

            var isValidTag = !logEntry.Tag.IsNullOrEmpty() &&
                             _configService != null &&
                             _configService.Iso8583Filter.Tag.Contains(logEntry.Tag);

            var isIsoMessageLog = !message.IsNullOrEmpty() &&
                                  message.Contains('|') &&
                                  ContainsValidKeywords(message);

            if (_isParsingMessage && isValidTag && !message.Contains('|')) FinalizeCurrentMessage();

            if (isValidTag && isIsoMessageLog)
            {
                Console.WriteLine($"Tag: {logEntry.Tag} - Message: {message}");
                if (_isParsingMessage) FinalizeCurrentMessage();
                _isParsingMessage = true;
            }

            if (_isParsingMessage && isValidTag) _currentMessageBuffer.Add(message);
        });
    }

    private void FinalizeCurrentMessage()
    {
        if (_currentMessageBuffer.Count == 0) return;

        var fullMessage = string.Join("\r\n", _currentMessageBuffer);

        Console.WriteLine($"fullMessage: {fullMessage}");
        var isoMsg = Iso8583Parser.ParseIsoMessage(fullMessage);

        _dispatcherQueue.TryEnqueue(() => AddMessageToTreeView(isoMsg));
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private void AddMessageToTreeView(ISO8583 isoMsg)
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
            Content = new HostRecord
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
        subfieldNode.Children.Add(new TreeViewNode { Content = CreateNewNode(emvData, color)});

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

    private static HostRecord CreateNewNode(string value, Brush color) => new() { Value = value, TextColor = color};


    private static HostRecord CreateNewNode(KeyValuePair<int,ISO8583DataElement> data, Brush color, string? length = null)
    {
        var values = data.Value.Value;

        var value = length != null
            ? $"{data.Key.ToString().PadLeft(3, '0')} : ({length})"
            : $"{data.Key.ToString().PadLeft(3, '0')} : {values[0]}";

        return new HostRecord
        {
            Tag = $"{data.Key.ToString().PadLeft(3, '0')}",
            Value = value,
            TextColor = color
        };
    }

    private static HostRecord CreateNewEmvNode(int key, int? length, Brush color)
    {
        var value = $"{key.ToString().PadLeft(3, '0')} : ({length})";

        return new HostRecord
        {
            Tag = "EMV DATA",
            Value = value,
            TextColor = color
        };
    }

    private static HostRecord CreateNewEmvNode(string? tag, int length, string? value, Brush color)
    {
        return new HostRecord
        {
            Tag = $"{tag,-8}",
            Value = $"{tag,-8}: ({length}) {value}",
            TextColor = color
        };
    }

    private static bool ContainsValidKeywords(string message) =>
        message.Contains("packRequest") || message.Contains("unpackResponse");
}
