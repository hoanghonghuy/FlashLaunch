using System;
using System.IO;
using FlashLaunch.Core.Utilities;
using FlashLaunch.Plugins.AppLauncher;

namespace FlashLaunch.UI.Services;

public sealed class MaintenanceService
{
    private readonly AppLauncherPlugin _appLauncherPlugin;
    private readonly IIconService _iconService;

    public MaintenanceService(AppLauncherPlugin appLauncherPlugin, IIconService iconService)
    {
        _appLauncherPlugin = appLauncherPlugin ?? throw new ArgumentNullException(nameof(appLauncherPlugin));
        _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
    }

    public void RefreshAppIndex() => _appLauncherPlugin.RefreshIndex();

    public void ResetUsageData() => _appLauncherPlugin.ResetUsageData();

    public bool TryClearLogs(out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            if (!Directory.Exists(AppDataPaths.LogsDirectory))
            {
                return true;
            }

            foreach (var file in Directory.EnumerateFiles(AppDataPaths.LogsDirectory, "*.log", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }
                catch (UnauthorizedAccessException ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }
            }

            _iconService.ClearMemoryCache();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryResetUsageAndLogs(out string? logError)
    {
        ResetUsageData();
        var logsCleared = TryClearLogs(out logError);
        return logsCleared;
    }

    public bool TryClearIconCache(out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var dir = AppDataPaths.IconCacheDirectory;
            if (!Directory.Exists(dir))
            {
                return true;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }
                catch (UnauthorizedAccessException ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }
            }

            _iconService.ClearMemoryCache();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
