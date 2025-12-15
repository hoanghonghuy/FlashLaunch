using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI.Services.Plugins;

public sealed class PluginCatalog : IPluginCatalog
{
    private readonly IEnumerable<IPlugin> _builtInPlugins;
    private readonly ExternalPluginLoader _externalPluginLoader;
    private readonly ILogger<PluginCatalog> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private IReadOnlyList<IPlugin>? _cached;
    private readonly object _gate = new();

    public PluginCatalog(IEnumerable<IPlugin> builtInPlugins, ExternalPluginLoader externalPluginLoader, ILogger<PluginCatalog> logger, ILoggerFactory loggerFactory)
    {
        _builtInPlugins = builtInPlugins ?? throw new ArgumentNullException(nameof(builtInPlugins));
        _externalPluginLoader = externalPluginLoader ?? throw new ArgumentNullException(nameof(externalPluginLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
            InitializeBuiltInHosts(builtIn);
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
        IReadOnlyList<IPlugin>? previous;

        lock (_gate)
        {
            previous = _cached;
            _cached = null;
            _logger.LogInformation("Plugin catalog cache cleared. Plugins will be re-scanned on next access.");
        }

        if (previous is not null)
        {
            DisposeAndUnloadExternalPlugins(previous);
        }
    }

    private void DisposeAndUnloadExternalPlugins(IReadOnlyList<IPlugin> plugins)
    {
        var contexts = new HashSet<AssemblyLoadContext>();
        var disposedCount = 0;

        foreach (var plugin in plugins)
        {
            var alc = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly);
            if (alc is null || alc == AssemblyLoadContext.Default || !alc.IsCollectible)
            {
                continue;
            }

            if (plugin is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                    disposedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose plugin during reload. Plugin={PluginType}", plugin.GetType().FullName);
                }
            }

            contexts.Add(alc);
        }

        var unloadCount = 0;
        var weakRefs = new List<WeakReference>();
        foreach (var context in contexts)
        {
            try
            {
                weakRefs.Add(new WeakReference(context));
                context.Unload();
                unloadCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unload plugin load context during reload. Context={Context}", context.Name);
            }
        }

        contexts.Clear();

        if (unloadCount > 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        var remaining = unloadCount > 0
            ? weakRefs.Count(wr => wr.IsAlive)
            : 0;

        _logger.LogInformation("Reload cleanup done. Disposed={DisposedCount} UnloadedContexts={UnloadedCount} RemainingContexts={Remaining}", disposedCount, unloadCount, remaining);
    }

    private void InitializeBuiltInHosts(IReadOnlyList<IPlugin> builtIn)
    {
        foreach (var plugin in builtIn)
        {
            if (plugin is not IPluginHostAware hostAware)
            {
                continue;
            }

            var pluginId = plugin is IPluginIdentity identity && !string.IsNullOrWhiteSpace(identity.Id)
                ? identity.Id
                : plugin.Name;

            var safeId = GetSafeDirectoryName(pluginId);
            var dataDir = Path.Combine(AppDataPaths.GetRoot(), "plugin-data", safeId);
            Directory.CreateDirectory(dataDir);
            var pluginLogger = _loggerFactory.CreateLogger($"BuiltInPlugin:{pluginId}");
            var host = new PluginHost(pluginId, AppContext.BaseDirectory, dataDir, pluginLogger);
            hostAware.Initialize(host);
        }
    }

    private static string GetSafeDirectoryName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
