using System.Collections.Generic;

namespace FlashLaunch.Core.Abstractions;

public interface IPluginCatalog
{
    IReadOnlyList<IPlugin> GetPlugins();
}
