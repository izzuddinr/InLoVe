using System;
using Microsoft.Extensions.DependencyInjection;
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
        MainAppWindow = new MainWindow();
        MainAppWindow.ExtendsContentIntoTitleBar = true;
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