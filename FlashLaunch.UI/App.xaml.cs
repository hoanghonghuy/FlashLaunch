using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Logging;
using FlashLaunch.Core.Services;
using FlashLaunch.Core.Utilities;
using FlashLaunch.Plugins.AppLauncher;
using FlashLaunch.Plugins.Calculator;
using FlashLaunch.Plugins.SystemCommands;
using FlashLaunch.Plugins.WebSearch;
using FlashLaunch.UI.Configuration;
using FlashLaunch.UI.Services;
using FlashLaunch.UI.Services.Plugins;
using FlashLaunch.UI.ViewModels;
using FlashLaunch.UI.Views;
using FlashLaunch.UI.Localization;
using FlashLaunch.UI.Theming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ShellController? _shellController;
    private TrayIconService? _trayIconService;
    private static readonly string LogsDirectory = AppDataPaths.LogsDirectory;

    private static readonly HashSet<string> PerfCategories = new(StringComparer.Ordinal)
    {
        typeof(MainViewModel).FullName!,
        typeof(QueryDispatcher).FullName!
    };

    private static readonly HashSet<string> PluginCategories = new(StringComparer.Ordinal)
    {
        typeof(ExternalPluginLoader).FullName!,
        typeof(PluginCatalog).FullName!,
        typeof(PluginLoadContext).FullName!
    };

    public IServiceProvider Services => _host!.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddConsole();
                logging.AddProvider(CreatePerfLogger());
                logging.AddProvider(CreatePluginLogger());
                logging.AddProvider(CreateErrorLogger());
            })
            .ConfigureServices((_, services) =>
            {
                services.AddLogging();
                services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
                services.AddSingleton<AppLauncherPlugin>();
                services.AddSingleton<IPlugin>(sp => sp.GetRequiredService<AppLauncherPlugin>());
                services.AddSingleton<IPlugin, CalculatorPlugin>();
                services.AddSingleton<IPlugin, WebSearchPlugin>();
                services.AddSingleton<IPlugin, SystemCommandsPlugin>();
                services.AddSingleton<IAppIndexPathProvider, AppIndexPathProvider>();

                services.AddSingleton<ExternalPluginLoader>();
                services.AddSingleton<IPluginCatalog, PluginCatalog>();

                services.AddSingleton<ConfigService>();
                services.AddSingleton<AppConfig>(_ =>
                {
                    var configService = _.GetRequiredService<ConfigService>();
                    return configService.Load();
                });
                services.AddSingleton<IPluginStateProvider, PluginStateProvider>();
                services.AddSingleton<MaintenanceService>();

                services.AddSingleton<IWebSearchProviderState, WebSearchProviderState>();
                services.AddSingleton<ISystemCommandState, SystemCommandState>();

                services.AddSingleton<IStringLocalizer, ResourceDictionaryLocalizer>();
                services.AddSingleton<IConfirmDialogService, ConfirmDialogService>();
                services.AddSingleton<IClipboardService, ClipboardService>();
                services.AddSingleton<IIconService, ShellIconService>();

                services.AddSingleton<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddTransient<SettingsWindow>();
                services.AddSingleton<TrayIconService>();
                services.AddSingleton<IHotkeyService, HotkeyService>();
                services.AddSingleton<ShellController>();
            })
            .Build();

        await _host.StartAsync();

        // Apply localization and theme resources based on configuration.
        var appConfig = Services.GetRequiredService<AppConfig>();
        LocalizationManager.ApplyLanguage(appConfig.Language);
        ThemeManager.ApplyTheme(appConfig.Theme);

        _shellController = Services.GetRequiredService<ShellController>();
        _shellController.Initialize();

        _trayIconService = Services.GetRequiredService<TrayIconService>();
        _trayIconService.Initialize();

        if (e.Args.Length > 0 && Array.Exists(e.Args, static arg => string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase)))
        {
            _shellController.ShowMainWindow();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _shellController?.Dispose();
        _trayIconService?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogUnhandledException("AppDomain", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogUnhandledException("TaskScheduler", e.Exception);
        e.SetObserved();
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandledException("Dispatcher", e.Exception);
        e.Handled = true;
    }

    private static void LogUnhandledException(string source, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            var logPath = Path.Combine(LogsDirectory, "flashlaunch-unhandled.log");

            var message = $"[{DateTime.Now:O}] {source} unhandled exception:{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, message);
        }
        catch
        {
            // ignore logging failures
        }
    }

    private static ILoggerProvider CreatePerfLogger()
    {
        var perfPath = Path.Combine(LogsDirectory, "flashlaunch-perf.log");
        return new FileLoggerProvider(perfPath, LogLevel.Information, (category, level) =>
            level == LogLevel.Information &&
            category is not null &&
            PerfCategories.Contains(category));
    }

    private static ILoggerProvider CreateErrorLogger()
    {
        var errorPath = Path.Combine(LogsDirectory, "flashlaunch-error.log");
        return new FileLoggerProvider(errorPath, LogLevel.Warning, static (_, level) => level >= LogLevel.Warning);
    }

    private static ILoggerProvider CreatePluginLogger()
    {
        var pluginPath = Path.Combine(LogsDirectory, "flashlaunch-plugin-loader.log");
        return new FileLoggerProvider(pluginPath, LogLevel.Debug, (category, level) =>
            level >= LogLevel.Debug &&
            category is not null &&
            PluginCategories.Contains(category));
    }
}
