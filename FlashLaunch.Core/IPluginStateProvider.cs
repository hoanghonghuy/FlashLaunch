namespace FlashLaunch.Core.Abstractions;

public interface IPluginStateProvider
{
    bool IsEnabled(string pluginName);

    void UpdateState(string pluginName, bool isEnabled);
}
