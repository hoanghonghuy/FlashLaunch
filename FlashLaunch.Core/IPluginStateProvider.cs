namespace FlashLaunch.Core.Abstractions;

public interface IPluginStateProvider
{
    bool IsEnabled(string pluginId, string? legacyKey = null);

    void UpdateState(string pluginId, bool isEnabled);
}
