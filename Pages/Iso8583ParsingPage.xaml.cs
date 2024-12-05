using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
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
    private readonly List<string> _currentMessageBuffer = [];

    private List<TreeView> _isoMsgTreeView = [];
    private List<ISO8583Msg> _isoMsg = [];
    private SolidColorBrush receiptTextColor;
    private bool _isParsingMessage;
    private string _currentParsingType = string.Empty;
    private Receipt? _currentReceipt = null;
    private static string receiptPattern = """CommandProxy:dispatch:\d+ transactionResult = \{"linePrintData".*""";
    private CancellationTokenSource? _processingCancellationTokenSource;


    private const string defaultFileExtension = ".png";

    public Iso8583ParsingPage()
    {
        InitializeComponent();
        IsoMsgScrollViewer.Background = ColorManager.GetBrush("AppBackgroundColor");
        ReceiptScrollViewer.Background = ColorManager.GetBrush("VerboseColor");
        ExportIsoMsgLogButton.Background = ColorManager.GetBrush("StopColor");
        receiptTextColor = ColorManager.GetBrush("AppBackgroundColor");

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

    private (bool IsValidTag, bool IsValidMessage, string MessageType) GetParsingFlags(LogEntry logEntry)
    {
        if (_configService == null)
        {
            return (false, false, string.Empty);
        }

        var message = logEntry.Message;
        var tag = logEntry.Tag;

        // Determine message type
        var isReceiptTag = tag?.Contains("APP_PRINT") == true;
        var isIsoTag = _configService.Iso8583Filter?.Tag.Contains(tag) == true;
        var isTagValid = !string.IsNullOrEmpty(tag);
        var isMessageValid = isReceiptTag
            ? ContainsValidKeywords(message)
            : !string.IsNullOrEmpty(message) && message.Contains('|') && ContainsValidKeywords(message);

        var messageType = isReceiptTag ? "RECEIPT_MSG" : "ISO_MSG";

        var isValidTag = isTagValid && (isReceiptTag || isIsoTag);

        return (isValidTag, isMessageValid, messageType);
    }


    private static bool ContainsValidKeywords(string message) =>
        ContainsValidIsoMsgKeywords(message) ||
        ContainsValidReceiptKeywords(message);

    private static bool ContainsValidIsoMsgKeywords(string message) =>
        message.Contains("packRequest") ||
        message.Contains("unpackResponse");

    private static bool ContainsValidReceiptKeywords(string message) =>
        message.StartsWith("PrinterImpl:savePreview:") &&
        message.Contains("savePreview: receipt:");

    private Task ProcessLogEntryAsync(LogEntry logEntry)
    {
        return Task.Run(() =>
        {
            var message = logEntry.Message;
            var (isValidTag, isValidMessage, parsingType) = GetParsingFlags(logEntry);

            var isValidParsingType = !string.IsNullOrEmpty(_currentParsingType)
                                     && _currentParsingType.Equals(parsingType);
            if (ShouldFinalizeMessage(parsingType, isValidParsingType, isValidTag, message))
            {
                if (parsingType == "RECEIPT_MSG")
                    _currentMessageBuffer.Add(message);
                FinalizeCurrentMessage(parsingType);
                return;
            }

            if (isValidTag && isValidMessage)
            {
                if (_isParsingMessage && isValidParsingType)
                {
                    FinalizeCurrentMessage(parsingType);
                }

                isValidParsingType = true;
                _isParsingMessage = true;
                _currentParsingType = parsingType;
            }

            if (_isParsingMessage && isValidParsingType && isValidTag)
            {
                _currentMessageBuffer.Add(message);
            }
        });
    }

    private bool ShouldFinalizeMessage(string parsingType, bool isValidParsingType, bool isValidTag, string message)
    {
        var shouldFinalize = parsingType switch
        {
            "RECEIPT_MSG" => _isParsingMessage
                             && isValidParsingType
                             && isValidTag
                             && (message.EndsWith("])])")
                                 || (message.StartsWith("PrinterImpl:savePreview:") && message.Contains(" Result: "))),
            "ISO_MSG" => _isParsingMessage
                         && isValidParsingType
                         && ((isValidTag && !message.Contains('|')) || !isValidTag),
            _ => false
        };
        return shouldFinalize;
    }

    private void FinalizeCurrentMessage(string parsingType = "ISO_MSG")
    {
        if (_currentMessageBuffer.Count <= 0) return;

        var fullMessage = string.Join("\r\n", _currentMessageBuffer);

        // if (_currentParsingType == "RECEIPT_MSG")
        // {
        //     Console.WriteLine(fullMessage);
        //     Console.WriteLine(new string('-', 20));
        // }

        switch (parsingType)
        {
            case "RECEIPT_MSG":
                // Console.WriteLine($"_currentMessageBuffer = {_currentMessageBuffer.Count}");
                var receipt = ReceiptParser.ParseFromJson(_currentMessageBuffer);
                _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (receipt != null)
                        {
                            ReceiptUIHelper.PopulateReceiptStackPanel(
                                receipt,
                                ReceiptStackPanel,
                                ReceiptStackPanel_OnRightTapped
                            );
                        }
                    }
                );
                Console.WriteLine("Finished receipt processing.");
                break;
            case "ISO_MSG":
                var isoMsg = Iso8583Parser.ParseIsoMessage(fullMessage);
                _dispatcherQueue.TryEnqueue(() => AddMessageToTreeView(isoMsg));
                Console.WriteLine("Finished ISO Msg processing.");
                break;
        }

        ResetParsingState();
    }

    private void ResetParsingState()
    {
        _currentMessageBuffer.Clear();
        _isParsingMessage = false;
        _currentParsingType = string.Empty;
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
            ? ColorManager.GetBrush("InfoColor")
            : ColorManager.GetBrush("WarningColor");

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
        _isoMsg.Add(isoMsg);

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
            await ExportToJsonFile(fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting JSON: {ex.Message}");
        }

    }

    private Task ExportToJsonFile(string filePath)
    {
        var isoMsgs = _isoMsg.ToList();

        // Serialize the filtered entries to JSON
        var jsonContent = JsonConvert.SerializeObject(isoMsgs, Formatting.Indented);

        // Write the JSON content to the specified file
        File.WriteAllText(filePath, jsonContent);

        return Task.CompletedTask;
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

    private async void ImportReceiptButton_OnClick(object sender, RoutedEventArgs e)
    {
        // try
        // {
        //     var openPicker = new FileOpenPicker
        //     {
        //         FileTypeFilter = { ".txt" },
        //         SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        //         ViewMode = PickerViewMode.List,
        //     };
        //     var window = App.MainAppWindow;
        //
        //     var hWnd = WindowNative.GetWindowHandle(window);
        //
        //     InitializeWithWindow.Initialize(openPicker, hWnd);
        //
        //     var file = await openPicker.PickSingleFileAsync();
        //     var fileContents = File.ReadAllLines(file.Path);
        //
        //     var receipt = ReceiptParser.ParseFromJson(fileContents.ToList());
        //     ReceiptUIHelper.PopulateReceiptStackPanel(receipt, ReceiptStackPanel);
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine("Error reading file: " + ex.Message);
        // }

        await SaveReceiptScrollViewerAsPictureAsync();
    }

    private async void ReceiptStackPanel_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        try
        {
            if (sender is not StackPanel stackPanel) return;

            var menuFlyoutItem = new MenuFlyoutItem
            {
                Text = "Save Receipt",
                Icon = new SymbolIcon(Symbol.Save),
                Tag = stackPanel
            };

            menuFlyoutItem.Click += ReceiptFlyOutButton_OnClick;

            var flyout = new MenuFlyout
            {
                Items =
                {
                    menuFlyoutItem
                }
            };

            var clickPosition = e.GetPosition(stackPanel);
            flyout.ShowAt(stackPanel, new FlyoutShowOptions
            {
                Position = clickPosition
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ReceiptStackPanel_OnRightTapped: {ex.StackTrace}");
        }
    }

    private async void ReceiptFlyOutButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuFlyoutItem { Tag: StackPanel stackPanel } ||
                await CreateOutputFolder() is not { } selectedFolder)
                return;

            await ExportReceipt(stackPanel, selectedFolder, defaultFileExtension);

            var dialog = CreateConfirmationDialog();
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                HandleOpenDirectory(selectedFolder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ReceiptFlyOutButton_OnClick: {ex.StackTrace}");
        }
    }

    private async Task SaveReceiptScrollViewerAsPictureAsync()
    {
        try
        {

            var selectedFolder = await CreateOutputFolder();
            var loadingDialog = CreateLoadingDialog();

            _ = loadingDialog.ShowAsync();

            foreach (var panel in ReceiptStackPanel.Children.OfType<StackPanel>())
            {
                await ExportReceipt(panel, selectedFolder, defaultFileExtension);
            }

            loadingDialog.Hide();
            loadingDialog = null;

            var dialog = CreateConfirmationDialog();
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                HandleOpenDirectory(selectedFolder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving ScrollViewer as picture: {ex.Message}");
        }
    }

    private static async Task ExportReceipt(StackPanel? panel, StorageFolder selectedFolder, string defaultFileExtension)
    {
        var suggestedFileName = $"RECEIPT_{panel.Name}{defaultFileExtension}";
        var file = await selectedFolder.CreateFileAsync(
            suggestedFileName,
            CreationCollisionOption.ReplaceExisting
        );

        if (file == null)
            return;

        await Task.Delay(100);

        // Rendering start
        var renderBitmap = new RenderTargetBitmap();
        await renderBitmap.RenderAsync(panel);

        var pixelBuffer = await renderBitmap.GetPixelsAsync();

        using var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)renderBitmap.PixelWidth,
            (uint)renderBitmap.PixelHeight,
            96,
            96,
            pixelBuffer.ToArray());

        await encoder.FlushAsync();
    }

    private async Task<StorageFolder?> CreateOutputFolder()
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var folderPath = Path.Combine(Environment.CurrentDirectory, "Receipts", timestamp);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.WriteLine($"Folder created: {folderPath}");
            }
            else
            {
                Debug.WriteLine($"Folder already exists: {folderPath}");
            }

            return await StorageFolder.GetFolderFromPathAsync(folderPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling directory: {ex.Message}");
            return null;
        }
    }

    private void HandleOpenDirectory(StorageFolder folder)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = folder.Path,
            UseShellExecute = true
        });
    }

    private ContentDialog CreateConfirmationDialog() => new()
    {
        Title = "Export Successful",
        Content =
            $"The file has been saved successfully." +
            $"{Environment.NewLine}" +
            $"Do you want to open the directory where the file was exported?",
        PrimaryButtonText = "Yes",
        CloseButtonText = "No",
        DefaultButton = ContentDialogButton.Primary,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        XamlRoot = App.MainAppWindow.Content.XamlRoot
    };

    private ContentDialog CreateLoadingDialog() => new()
    {
        Content = new StackPanel()
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0),
            Padding = new Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Text = "Please wait while we export the receipts...",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 5)
                },
                new ProgressBar
                {
                    Height = 20l,
                    IsIndeterminate = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(20, 5, 20, 10)
                }
            }
        },
        XamlRoot = App.MainAppWindow.Content.XamlRoot,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        IsPrimaryButtonEnabled = false,
        IsSecondaryButtonEnabled = false
    };
}