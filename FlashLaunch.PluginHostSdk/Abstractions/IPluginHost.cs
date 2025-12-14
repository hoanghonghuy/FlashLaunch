using Microsoft.Extensions.Logging;

namespace FlashLaunch.Core.Abstractions;

public interface IPluginHost
{
    string PluginId { get; }

    string PluginDirectory { get; }

    string DataDirectory { get; }

    ILogger Logger { get; }

    void OpenUrl(string url);

    void OpenPath(string path) => OpenUrl(path);

    bool TryStartProcess(string fileName, string? arguments = null) => false;
}
