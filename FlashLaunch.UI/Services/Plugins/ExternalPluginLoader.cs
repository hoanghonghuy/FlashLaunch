using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Utilities;

namespace FlashLaunch.UI.Services.Plugins;

public sealed class ExternalPluginLoader
{
    private const string ManifestFileName = "plugin.json";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            result.AddRange(LoadPluginsFromRoot(root));
        }

        return result;
    }

    private static IEnumerable<IPlugin> LoadPluginsFromRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            yield break;
        }

        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var pluginDir in Directory.EnumerateDirectories(root))
        {
            var manifestPath = Path.Combine(pluginDir, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var plugin = TryLoadPlugin(pluginDir, manifestPath);
            if (plugin is not null)
            {
                yield return plugin;
            }
        }
    }

    private static IPlugin? TryLoadPlugin(string pluginDir, string manifestPath)
    {
        IPlugin? plugin = null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ExternalPluginManifest>(json, ManifestJsonOptions);
            if (manifest is null)
            {
                return null;
            }

            var assemblyPath = Path.Combine(pluginDir, manifest.Assembly);
            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            var loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(manifest.Type, throwOnError: false, ignoreCase: false);
            if (type is null)
            {
                return null;
            }

            if (!typeof(IPlugin).IsAssignableFrom(type))
            {
                return null;
            }

            plugin = Activator.CreateInstance(type) as IPlugin;
            if (plugin is null)
            {
                return null;
            }

            if (plugin is IPluginIdentity identity &&
                !string.IsNullOrWhiteSpace(identity.Id) &&
                !string.Equals(identity.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return plugin;
        }
        catch
        {
            if (plugin is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }

            return null;
        }
    }
}
