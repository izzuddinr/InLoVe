using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Qatalyst.Services;
using UnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace Qatalyst;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }
    public static Window MainAppWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        ConfigureServices();
    }

    private void ConfigureServices()
    {

        var serviceCollection = new ServiceCollection();

        // Register AdbProcessManager as a singleton
        serviceCollection.AddSingleton<AdbProcessManager>();

        // Register existing services
        serviceCollection.AddSingleton<ConfigService>();
        serviceCollection.AddSingleton<LogStorageService>();
        serviceCollection.AddSingleton<PackageNameService>();
        serviceCollection.AddSingleton<PubSubService>();

        serviceCollection.AddSingleton<DeviceService>();
        serviceCollection.AddSingleton<LogcatService>();

        Services = serviceCollection.BuildServiceProvider();

        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ShowDisclaimerMessageBox();
    }

    private void ShowDisclaimerMessageBox()
    {
        DisclaimerWindow disclaimerWindow = new DisclaimerWindow();
        disclaimerWindow.ExtendsContentIntoTitleBar = true;
        disclaimerWindow.Closed += DisclaimerWindowOnClosed;
        disclaimerWindow.Activate();
    }

    private void DisclaimerWindowOnClosed(object sender, WindowEventArgs args)
    {
        LoadMainWindow();
    }

    private void LoadMainWindow()
    {
        MainAppWindow = new MainWindow();
        MainAppWindow.ExtendsContentIntoTitleBar = true;

        if (MainAppWindow.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        MainAppWindow.Closed += MainAppWindowOnClosed;
        MainAppWindow.Activate();
    }

    private void MainAppWindowOnClosed(object sender, WindowEventArgs args)
    {
        Services.GetService<AdbProcessManager>()?.Dispose();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Services.GetService<AdbProcessManager>()?.Dispose();
    }
}