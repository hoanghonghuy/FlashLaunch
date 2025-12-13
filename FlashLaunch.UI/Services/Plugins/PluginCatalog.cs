using System;
using System.Collections.Generic;
using System.Linq;
using FlashLaunch.Core.Abstractions;

namespace FlashLaunch.UI.Services.Plugins;

public sealed class PluginCatalog : IPluginCatalog
{
    private readonly IEnumerable<IPlugin> _builtInPlugins;
    private readonly ExternalPluginLoader _externalPluginLoader;
    private IReadOnlyList<IPlugin>? _cached;
    private readonly object _gate = new();

    public PluginCatalog(IEnumerable<IPlugin> builtInPlugins, ExternalPluginLoader externalPluginLoader)
    {
        _builtInPlugins = builtInPlugins ?? throw new ArgumentNullException(nameof(builtInPlugins));
        _externalPluginLoader = externalPluginLoader ?? throw new ArgumentNullException(nameof(externalPluginLoader));
    }

    public IReadOnlyList<IPlugin> GetPlugins()
    {
        lock (_gate)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var list = new List<IPlugin>();
            list.AddRange(_builtInPlugins);
            list.AddRange(_externalPluginLoader.LoadPlugins());
            _cached = list;
            return _cached;
        }
    }
}
