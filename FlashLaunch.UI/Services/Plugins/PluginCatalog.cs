using System;
using System.Collections.Generic;
using System.Linq;
using FlashLaunch.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI.Services.Plugins;

public sealed class PluginCatalog : IPluginCatalog
{
    private readonly IEnumerable<IPlugin> _builtInPlugins;
    private readonly ExternalPluginLoader _externalPluginLoader;
    private readonly ILogger<PluginCatalog> _logger;
    private IReadOnlyList<IPlugin>? _cached;
    private readonly object _gate = new();

    public PluginCatalog(IEnumerable<IPlugin> builtInPlugins, ExternalPluginLoader externalPluginLoader, ILogger<PluginCatalog> logger)
    {
        _builtInPlugins = builtInPlugins ?? throw new ArgumentNullException(nameof(builtInPlugins));
        _externalPluginLoader = externalPluginLoader ?? throw new ArgumentNullException(nameof(externalPluginLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<IPlugin> GetPlugins()
    {
        lock (_gate)
        {
            if (_cached is not null)
            {
                _logger.LogDebug("Returning cached plugin list. Count={PluginCount}", _cached.Count);
                return _cached;
            }

            var list = new List<IPlugin>();
            var builtIn = _builtInPlugins.ToList();
            var external = _externalPluginLoader.LoadPlugins();
            list.AddRange(builtIn);
            list.AddRange(external);

            _logger.LogInformation("Plugin catalog built. BuiltIn={BuiltInCount} External={ExternalCount} Total={TotalCount}",
                builtIn.Count,
                external.Count,
                list.Count);
            _cached = list;
            return _cached;
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _cached = null;
            _logger.LogInformation("Plugin catalog cache cleared. Plugins will be re-scanned on next access.");
        }
    }
}
