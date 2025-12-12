namespace FlashLaunch.Core.Abstractions;

public interface ISystemCommandState
{
    bool IsCommandEnabled(string key);
}
