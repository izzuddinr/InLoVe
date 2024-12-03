using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Qatalyst.Objects;
using Qatalyst.Services;
using Qatalyst.Utils;

namespace Qatalyst.Pages;

public sealed partial class DevicePage : Page
{
    private readonly DeviceService? _deviceService;
    private readonly PubSubService? _pubSubService;

    public ObservableCollection<string> DeviceConsoleHistory = [];
    public ObservableCollection<DeviceInfo> DeviceList = [];

    private DeviceInfo? _currentDeviceInfo = null;

    private readonly DispatcherQueue _dispatcherQueue;

    private int _currentCommandIndex = -1;

    public DevicePage()
    {
        InitializeComponent();
        _deviceService = App.Services.GetService<DeviceService>();
        _pubSubService = App.Services.GetService<PubSubService>();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ContentGrid.Background = ColorManager.GetBrush(ApplicationColor.AppBackgroundColor.ToString());
        ConsoleHistoryListView.Background = new SolidColorBrush(Colors.Black);

        LoadDevices();
    }

    private async void LoadDevices()
    {
        if (_deviceService == null) return;

        _dispatcherQueue.TryEnqueue(async void () =>
        {
            try
            {
                DeviceList.Clear();

                Console.WriteLine("Loading devices...");
                var devices = await _deviceService.GetConnectedDevicesWithDetails();
                foreach (var deviceInfo in devices)
                {
                    DeviceList.Add(deviceInfo);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while loading devices: {e.Message} {e.StackTrace}");
            }
        });
    }

    private void AddNewLog(string newLog)
    {
        var formattedLog = $@"{DateTime.Now:MM\-dd\ HH\:mm\:ss\.ffff} : {newLog}";
        DeviceConsoleHistory.Add(formattedLog);
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        var lastItem = ConsoleHistoryListView.Items.LastOrDefault();
        if (lastItem != null)
        {
            ConsoleHistoryListView.ScrollIntoView(lastItem);
        }
    }

    private void DoSendTextInput(string command)
    {
        _deviceService.RunAdbCommand(command);
        AddNewLog($"\"{command}\" sent to device.");
    }

    private void DoScreenshot(string? filename = null)
    {
        if (_deviceService == null || _currentDeviceInfo?.SerialNumber == null) return;

        var serialNumber = _currentDeviceInfo.SerialNumber;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var screenshotFileName = filename is null ? $"screenshot_{timestamp}.png" : $"{filename}.png";
        var devicePath = $"/sdcard/{screenshotFileName}";
        var pcPath = $@"{Environment.CurrentDirectory}Screenshots\{screenshotFileName}";

        _deviceService.RunAdbCommand($"-s {serialNumber} shell screencap -p {devicePath}");
        _deviceService.RunAdbCommand($"-s {serialNumber} pull {devicePath} {pcPath}");
        _deviceService.RunAdbCommand($"-s {serialNumber} shell rm {devicePath}");

        AddNewLog($"Screenshot saved to {pcPath}");
    }

    private void DeviceCommandInputBox_OnQuerySubmitted(AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        try
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                sender.Text = string.Empty;

                switch (_currentCommandIndex)
                {
                    case 0:
                        DoSendTextInput($"shell input text \"{args.QueryText}\"");
                        break;
                    case 1:
                        DoScreenshot(string.IsNullOrWhiteSpace(args.QueryText) ? null : args.QueryText);
                        break;
                    default:
                        throw new InvalidOperationException("Invalid command index");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while processing query: {ex.Message}");
        }
    }

    private void CommandButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;

        _currentCommandIndex = int.Parse(tag);
        DeviceCommandInputBox.PlaceholderText = _currentCommandIndex switch
        {
            -1 => "Enter command text (e.g. adb shell input text \"input_text\")",
            0 => "Enter Text (e.g. 5012345678910)",
            1 => "Enter Filename (e.g. screenshot_1 => screenshot_1.png)",
        };
    }

    private void DeviceGridView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentDeviceInfo = (DeviceInfo)e.AddedItems[0];
        Console.WriteLine($"Current Device Info: {Environment.NewLine}{_currentDeviceInfo}");
        _pubSubService?.Publish("DeviceSelected", _currentDeviceInfo);
    }
}
