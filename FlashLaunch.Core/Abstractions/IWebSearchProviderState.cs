namespace FlashLaunch.Core.Abstractions;

public interface IWebSearchProviderState
{
    bool IsProviderEnabled(string prefix);
}
