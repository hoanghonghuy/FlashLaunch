using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI.Services.Plugins;

public sealed class ExternalPluginLoader
{
    private const string ManifestFileName = "plugin.json";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<ExternalPluginLoader> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ExternalPluginLoader(ILogger<ExternalPluginLoader> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IReadOnlyList<IPlugin> LoadPlugins()
    {
        var roots = new[]
        {
            AppDataPaths.PluginsDirectory,
            Path.Combine(AppContext.BaseDirectory, "plugins")
        };

        var result = new List<IPlugin>();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Scanning external plugins from: {Root}", root);
            result.AddRange(LoadPluginsFromRoot(root));
        }

        _logger.LogInformation("Loaded {PluginCount} external plugins.", result.Count);

        return result;
    }

    private IEnumerable<IPlugin> LoadPluginsFromRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            _logger.LogDebug("Plugin root is empty; skipping.");
            yield break;
        }

        if (!Directory.Exists(root))
        {
            _logger.LogDebug("Plugin root does not exist; skipping. Root={Root}", root);
            yield break;
        }

        foreach (var pluginDir in Directory.EnumerateDirectories(root))
        {
            var manifestPath = Path.Combine(pluginDir, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug("Skipping plugin directory (missing {ManifestFile}). Dir={Dir}", ManifestFileName, pluginDir);
                continue;
            }

            var plugin = TryLoadPlugin(pluginDir, manifestPath);
            if (plugin is not null)
            {
                _logger.LogInformation("Loaded external plugin: {PluginName} ({PluginType}) from {Dir}", plugin.Name, plugin.GetType().FullName, pluginDir);
                yield return plugin;
            }
        }
    }

    private IPlugin? TryLoadPlugin(string pluginDir, string manifestPath)
    {
        IPlugin? plugin = null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ExternalPluginManifest>(json, ManifestJsonOptions);
            if (manifest is null)
            {
                _logger.LogWarning("Failed to parse plugin manifest. Path={ManifestPath}", manifestPath);
                return null;
            }

            if (manifest.ApiVersion != 1)
            {
                _logger.LogWarning("Unsupported plugin apiVersion. Path={ManifestPath} ApiVersion={ApiVersion}", manifestPath, manifest.ApiVersion);
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Assembly) || string.IsNullOrWhiteSpace(manifest.Type))
            {
                _logger.LogWarning("Invalid plugin manifest (missing fields). Path={ManifestPath} Id={Id} Assembly={Assembly} Type={Type}",
                    manifestPath,
                    manifest.Id,
                    manifest.Assembly,
                    manifest.Type);
                return null;
            }

            var assemblyPath = Path.Combine(pluginDir, manifest.Assembly);
            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning("Plugin assembly not found. Dir={Dir} Assembly={Assembly} ResolvedPath={AssemblyPath}", pluginDir, manifest.Assembly, assemblyPath);
                return null;
            }

            var loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(manifest.Type, throwOnError: false, ignoreCase: false);
            if (type is null)
            {
                _logger.LogWarning("Plugin type not found in assembly. Assembly={AssemblyPath} Type={Type}", assemblyPath, manifest.Type);
                return null;
            }

            if (!typeof(IPlugin).IsAssignableFrom(type))
            {
                _logger.LogWarning("Plugin type does not implement IPlugin. Assembly={AssemblyPath} Type={Type}", assemblyPath, type.FullName);
                return null;
            }

            plugin = Activator.CreateInstance(type) as IPlugin;
            if (plugin is null)
            {
                _logger.LogWarning("Failed to create plugin instance. Type={Type}", type.FullName);
                return null;
            }

            if (plugin is IPluginIdentity identity &&
                !string.IsNullOrWhiteSpace(identity.Id) &&
                !string.Equals(identity.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Plugin identity mismatch. ManifestId={ManifestId} PluginId={PluginId} Type={Type}", manifest.Id, identity.Id, type.FullName);
                return null;
            }

            if (plugin is IPluginHostAware hostAware)
            {
                var safeId = GetSafeDirectoryName(manifest.Id);
                var dataDir = Path.Combine(AppDataPaths.GetRoot(), "plugin-data", safeId);
                Directory.CreateDirectory(dataDir);
                var pluginLogger = _loggerFactory.CreateLogger($"ExternalPlugin:{manifest.Id}");
                var host = new PluginHost(manifest.Id, pluginDir, dataDir, pluginLogger);
                hostAware.Initialize(host);
            }

            return plugin;
        }
        catch (Exception ex)
        {
            if (plugin is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }

            _logger.LogError(ex, "Failed to load external plugin. Dir={Dir} Manifest={ManifestPath}", pluginDir, manifestPath);

            return null;
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
