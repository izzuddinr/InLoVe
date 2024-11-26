﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Qatalyst.Services;

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

        // Register services
        serviceCollection.AddSingleton<ConfigService>();
        serviceCollection.AddSingleton<DeviceService>();
        serviceCollection.AddSingleton<LogcatService>();
        serviceCollection.AddSingleton<LogStorageService>();
        serviceCollection.AddSingleton<PackageNameService>();
        serviceCollection.AddSingleton<PubSubService>();

        Services = serviceCollection.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainAppWindow = new MainWindow();
        MainAppWindow.ExtendsContentIntoTitleBar = true;

        MainAppWindow.Activate();
    }
}