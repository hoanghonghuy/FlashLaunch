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

    private static readonly string[] SharedAssemblyFileNames =
    {
        "FlashLaunch.Core.dll",
        "FlashLaunch.PluginSdk.dll",
        "FlashLaunch.PluginHostSdk.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll"
    };

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
        var roots = new List<string>
        {
            AppDataPaths.PluginsDirectory,
            Path.Combine(AppContext.BaseDirectory, "plugins")
        };

        roots.AddRange(GetDevPluginRoots());

        var result = new List<IPlugin>();
        var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Scanning external plugins from: {Root}", root);
            result.AddRange(LoadPluginsFromRoot(root, loadedIds));
        }

        _logger.LogInformation("Loaded {PluginCount} external plugins.", result.Count);

        return result;
    }

    private IEnumerable<string> GetDevPluginRoots()
    {
        var result = new List<string>();

        var defaultDev = Path.Combine(AppDataPaths.GetRoot(), "plugins-dev");
        result.Add(defaultDev);

        var env = Environment.GetEnvironmentVariable("FLASHLAUNCH_DEV_PLUGINS_DIR");
        if (string.IsNullOrWhiteSpace(env))
        {
            return result;
        }

        foreach (var value in env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                result.Add(Path.GetFullPath(value));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve dev plugin root from environment variable. Value={Value}", value);
            }
        }

        return result;
    }

    private IEnumerable<IPlugin> LoadPluginsFromRoot(string root, HashSet<string> loadedIds)
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

            var loaded = TryLoadPlugin(pluginDir, manifestPath);
            if (loaded is not null)
            {
                if (!loadedIds.Add(loaded.Id))
                {
                    if (loaded.Plugin is IDisposable disposable)
                    {
                        try { disposable.Dispose(); } catch { }
                    }

                    try { loaded.LoadContext.Unload(); } catch { }

                    _logger.LogWarning("Duplicate external plugin id detected; skipping. Id={PluginId} Dir={Dir}", loaded.Id, pluginDir);
                    continue;
                }

                _logger.LogInformation("Loaded external plugin: {PluginName} ({PluginType}) Id={PluginId} from {Dir}", loaded.Plugin.Name, loaded.Plugin.GetType().FullName, loaded.Id, pluginDir);
                yield return loaded.Plugin;
            }
        }
    }

    private sealed record LoadedExternalPlugin(string Id, IPlugin Plugin, PluginLoadContext LoadContext);

    private LoadedExternalPlugin? TryLoadPlugin(string pluginDir, string manifestPath)
    {
        IPlugin? plugin = null;
        PluginLoadContext? loadContext = null;
        ExternalPluginManifest? manifest = null;
        try
        {
            if (!TryReadManifest(manifestPath, out var parsedManifest))
            {
                return null;
            }

            manifest = parsedManifest;

            if (!ValidateManifest(manifestPath, manifest, out var validationError))
            {
                _logger.LogWarning("Invalid plugin manifest. Path={ManifestPath} Error={Error}", manifestPath, validationError);
                return null;
            }

            WarnIfBundledSharedAssemblies(pluginDir, manifest.Id);

            var pluginDirFull = Path.GetFullPath(pluginDir);
            var assemblyPath = Path.GetFullPath(Path.Combine(pluginDir, manifest.Assembly));
            if (!assemblyPath.StartsWith(pluginDirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assemblyPath, pluginDirFull, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Plugin assembly resolved outside plugin directory; skipping. Dir={Dir} Assembly={Assembly} ResolvedPath={AssemblyPath}", pluginDir, manifest.Assembly, assemblyPath);
                return null;
            }

            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning("Plugin assembly not found. Dir={Dir} Assembly={Assembly} ResolvedPath={AssemblyPath}", pluginDir, manifest.Assembly, assemblyPath);
                return null;
            }

            loadContext = new PluginLoadContext(assemblyPath);
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
                if (plugin is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch { }
                }

                plugin = null;
                return null;
            }

            if (plugin is IPluginHostAware hostAware)
            {
                var safeId = GetSafeDirectoryName(manifest.Id);
                var dataDir = Path.Combine(AppDataPaths.GetRoot(), "plugin-data", safeId);
                Directory.CreateDirectory(dataDir);
                var pluginLogger = _loggerFactory.CreateLogger($"ExternalPlugin:{manifest.Id}");
                var host = new PluginHost(manifest.Id, pluginDir, dataDir, pluginLogger);
                try
                {
                    hostAware.Initialize(host);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize external plugin host context. Id={PluginId} Type={Type} Dir={Dir}", manifest.Id, type.FullName, pluginDir);
                    if (plugin is IDisposable disposable)
                    {
                        try { disposable.Dispose(); } catch { }
                    }

                    plugin = null;
                    return null;
                }
            }

            return new LoadedExternalPlugin(manifest.Id, plugin, loadContext);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse plugin manifest JSON. Dir={Dir} Manifest={ManifestPath}", pluginDir, manifestPath);
            return null;
        }
        catch (BadImageFormatException ex)
        {
            _logger.LogError(ex, "Plugin assembly is not a valid .NET assembly. Dir={Dir} Manifest={ManifestPath}", pluginDir, manifestPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load external plugin. Dir={Dir} Manifest={ManifestPath}", pluginDir, manifestPath);
            return null;
        }
        finally
        {
            if (plugin is null && loadContext is not null)
            {
                try { loadContext.Unload(); } catch { }
            }
        }
    }

    private bool TryReadManifest(string manifestPath, out ExternalPluginManifest manifest)
    {
        manifest = null!;

        try
        {
            var json = File.ReadAllText(manifestPath);
            var parsed = JsonSerializer.Deserialize<ExternalPluginManifest>(json, ManifestJsonOptions);
            if (parsed is null)
            {
                _logger.LogWarning("Failed to parse plugin manifest. Path={ManifestPath}", manifestPath);
                return false;
            }

            manifest = parsed;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read plugin manifest. Path={ManifestPath}", manifestPath);
            return false;
        }
    }

    private static bool ValidateManifest(string manifestPath, ExternalPluginManifest manifest, out string error)
    {
        error = string.Empty;

        if (manifest.ApiVersion != 1)
        {
            error = $"Unsupported apiVersion={manifest.ApiVersion}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            error = "Missing 'id'.";
            return false;
        }

        manifest.Id = manifest.Id.Trim();

        if (!IsValidPluginId(manifest.Id))
        {
            error = $"Invalid plugin id '{manifest.Id}'. Allowed chars: [A-Za-z0-9._-]";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            error = "Missing 'assembly'.";
            return false;
        }

        manifest.Assembly = manifest.Assembly.Trim();
        if (!string.Equals(manifest.Assembly, Path.GetFileName(manifest.Assembly), StringComparison.Ordinal))
        {
            error = "'assembly' must be a file name (no directories).";
            return false;
        }

        if (!manifest.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            error = "'assembly' must point to a .dll file.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Type))
        {
            error = "Missing 'type'.";
            return false;
        }

        manifest.Type = manifest.Type.Trim();
        if (manifest.Type.IndexOf('`') >= 0)
        {
            error = "Generic types are not supported in manifest 'type'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            error = "Invalid manifest path.";
            return false;
        }

        return true;
    }

    private void WarnIfBundledSharedAssemblies(string pluginDir, string pluginId)
    {
        foreach (var fileName in SharedAssemblyFileNames)
        {
            var path = Path.Combine(pluginDir, fileName);
            if (File.Exists(path))
            {
                _logger.LogWarning("Plugin directory contains a host-shared assembly '{AssemblyFile}'. It will be ignored (loaded from default context). Consider removing it. Id={PluginId} Dir={Dir}", fileName, pluginId, pluginDir);
            }
        }
    }

    private static bool IsValidPluginId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (id.Length > 100)
        {
            return false;
        }

        foreach (var ch in id)
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')
            {
                continue;
            }

            return false;
        }

        return true;
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
