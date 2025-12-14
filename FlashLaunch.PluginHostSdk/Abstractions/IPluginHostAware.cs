namespace FlashLaunch.Core.Abstractions;

public interface IPluginHostAware
{
    void Initialize(IPluginHost host);
}
