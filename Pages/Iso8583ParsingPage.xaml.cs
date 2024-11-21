using System;
using System.Collections.Generic;
using InLoVe.Objects;
using InLoVe.Services;
using InLoVe.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace InLoVe.Pages;

public partial class Iso8583ParsingPage
{
    private readonly PubSubService? _pubSubService;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly List<string> _currentMessageBuffer = [];

    private bool _isParsingMessage;

    public Iso8583ParsingPage()
    {
        InitializeComponent();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _pubSubService = App.Services.GetService<PubSubService>();
        _pubSubService?.Subscribe("LogEntrySaved", OnLogEntryReceived);
    }

    private void OnLogEntryReceived(object eventData)
    {
        if (eventData is not LogEntry logEntry || string.IsNullOrWhiteSpace(logEntry.Message))
            return;

        _dispatcherQueue.TryEnqueue(() => { ProcessLogEntry(logEntry.Message); });
    }

    private void ProcessLogEntry(string message)
    {
        if (!message.Contains('|'))
        {
            FinalizeCurrentMessage();
        }

        if ((message.Contains("packRequest") || message.Contains("unpackResponse")) && message.Contains('|'))
        {
            if (_isParsingMessage)
            {
                FinalizeCurrentMessage();
            }

            _isParsingMessage = true;
        }

        if (!_isParsingMessage) return;

        _currentMessageBuffer.Add(message);
    }

    private void FinalizeCurrentMessage()
    {
        if (_currentMessageBuffer.Count == 0) return;

        var fullMessage = string.Join("\n", _currentMessageBuffer);
        var (mti, parsedFields) = Iso8583Parser.ParseIsoMessage(fullMessage);

        AddMessageToTreeView(mti, parsedFields);
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
    }

    private void AddMessageToTreeView(string mti, Dictionary<string, string> fields)
    {
        // Create a new TreeView for this ISO8583 message
        var newTreeView = new TreeView
        {
            Margin = new Microsoft.UI.Xaml.Thickness(5),
        };

        // Create the root node using the MTI
        var rootNode = new TreeViewNode
        {
            Content = $"MTI: {mti}"
        };

        // Add fields to the root node
        foreach (var field in fields)
        {
            if (field.Key.Contains('('))
            {
                var mainField = field.Key.Split('(')[0].Trim(); // e.g., 60
                var subfieldNode = new TreeViewNode
                {
                    Content = $"{mainField} ({field.Key.Split('(')[1]}"
                };

                subfieldNode.Children.Add(new TreeViewNode { Content = field.Value });
                rootNode.Children.Add(subfieldNode);
            }
            else
            {
                var fieldNode = new TreeViewNode
                {
                    Content = $"{field.Key}={field.Value}"
                };
                rootNode.Children.Add(fieldNode);
            }
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
}