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

    public bool IsEnabled(string pluginId, string? legacyKey = null)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        if (_config.PluginStates.TryGetValue(pluginId, out var state))
        {
            return state;
        }

        if (!string.IsNullOrWhiteSpace(legacyKey) &&
            _config.PluginStates.TryGetValue(legacyKey, out state))
        {
            _config.PluginStates[pluginId] = state;
            return state;
        }

        return true;
    }

    public void UpdateState(string pluginId, bool isEnabled)
    {
        _config.PluginStates[pluginId] = isEnabled;
    }
}
