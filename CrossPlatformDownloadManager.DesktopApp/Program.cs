﻿using Avalonia;
using Avalonia.ReactiveUI;
using System;
using CrossPlatformDownloadManager.Data.Services.AppService;
using CrossPlatformDownloadManager.Data.Services.DownloadFileService;
using CrossPlatformDownloadManager.Data.Services.DownloadQueueService;
using CrossPlatformDownloadManager.Data.Services.SettingsService;
using CrossPlatformDownloadManager.Data.Services.UnitOfWork;
using CrossPlatformDownloadManager.DesktopApp.Infrastructure;
using CrossPlatformDownloadManager.DesktopApp.Infrastructure.AppInitializer;
using CrossPlatformDownloadManager.DesktopApp.ViewModels;
using CrossPlatformDownloadManager.DesktopApp.Views;
using Microsoft.Extensions.DependencyInjection;
using RolandK.AvaloniaExtensions.DependencyInjection;

namespace CrossPlatformDownloadManager.DesktopApp;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var appBuilder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI()
            .UseDependencyInjection(services =>
            {
                // Add AutoMapper to services
                services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
                // Add UnitOfWork to services
                services.AddTransient<IUnitOfWork, UnitOfWork>();
                // Add DownloadFileService to services
                services.AddSingleton<IDownloadFileService, DownloadFileService>();
                // Add DownloadQueueService to services
                services.AddSingleton<IDownloadQueueService, DownloadQueueService>();
                // Add SettingsService to services
                services.AddSingleton<ISettingsService, SettingsService>();
                // Add AppService to services
                services.AddSingleton<IAppService, AppService>();
                // Add ViewModels to services
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<TrayMenuWindowViewModel>();
                // Add Windows to services
                services.AddSingleton<MainWindow>();
                services.AddSingleton<TrayMenuWindow>();
                // Add AppInitializer to services
                services.AddSingleton<IAppInitializer, AppInitializer>();
            })
            .InitializeApp();

        return appBuilder;
    }
}