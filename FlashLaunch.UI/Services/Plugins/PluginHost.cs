using System;
using System.Diagnostics;
using FlashLaunch.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI.Services.Plugins;

internal sealed class PluginHost : IPluginHost
{
    public PluginHost(string pluginId, string pluginDirectory, string dataDirectory, ILogger logger)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        DataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string PluginId { get; }

    public string PluginDirectory { get; }

    public string DataDirectory { get; }

    public ILogger Logger { get; }

    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public bool TryStartProcess(string fileName, string? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            return Process.Start(startInfo) is not null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start process. FileName={FileName} Args={Args}", fileName, arguments);
            return false;
        }
    }
}
