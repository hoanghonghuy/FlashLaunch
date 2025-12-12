using FlashLaunch.Core.Abstractions;
using FlashLaunch.UI.Configuration;

namespace FlashLaunch.UI.Services;

public sealed class PluginStateProvider : IPluginStateProvider
{
    private readonly AppConfig _config;

    public PluginStateProvider(AppConfig config)
    {
        _config = config;
    }

    public bool IsEnabled(string pluginName)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
        {
            return false;
        }

        if (_config.PluginStates.TryGetValue(pluginName, out var state))
        {
            return state;
        }

        return true;
    }

    public void UpdateState(string pluginName, bool isEnabled)
    {
        _config.PluginStates[pluginName] = isEnabled;
    }
}
