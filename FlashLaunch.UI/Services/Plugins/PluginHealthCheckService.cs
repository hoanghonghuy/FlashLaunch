using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.UI.Services.Plugins;

public sealed record PluginHealthCheckResult(string PluginId, string PluginDirectory, bool Passed, string? Error);

public sealed record PluginHealthCheckSummary(int Total, int Passed, int Failed, IReadOnlyList<PluginHealthCheckResult> Results);

public sealed class PluginHealthCheckService
{
    private const string ManifestFileName = "plugin.json";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<PluginHealthCheckService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public PluginHealthCheckService(ILogger<PluginHealthCheckService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public async Task<PluginHealthCheckSummary> RunExternalPluginsAsync(TimeSpan timeoutPerPlugin, CancellationToken cancellationToken = default)
    {
        var roots = new List<string>
        {
            AppDataPaths.PluginsDirectory,
            Path.Combine(AppContext.BaseDirectory, "plugins")
        };

        roots.AddRange(GetDevPluginRoots());

        var results = new List<PluginHealthCheckResult>();

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var pluginDir in Directory.EnumerateDirectories(root))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var manifestPath = Path.Combine(pluginDir, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var check = await CheckExternalPluginAsync(pluginDir, manifestPath, timeoutPerPlugin, cancellationToken);
                results.Add(check);
            }
        }

        var total = results.Count;
        var passed = results.Count(r => r.Passed);
        var failed = total - passed;

        return new PluginHealthCheckSummary(total, passed, failed, results);
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
            catch
            {
            }
        }

        return result;
    }

    private async Task<PluginHealthCheckResult> CheckExternalPluginAsync(string pluginDir, string manifestPath, TimeSpan timeoutPerPlugin, CancellationToken cancellationToken)
    {
        var pluginId = string.Empty;

        IPlugin? plugin = null;
        PluginLoadContext? loadContext = null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<ExternalPluginManifest>(json, ManifestJsonOptions);
            if (manifest is null)
            {
                return new PluginHealthCheckResult("(unknown)", pluginDir, false, "Manifest JSON is invalid.");
            }

            pluginId = string.IsNullOrWhiteSpace(manifest.Id) ? "(unknown)" : manifest.Id;

            if (!ValidateManifest(manifest, out var manifestError))
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, manifestError);
            }

            var pluginDirFull = Path.GetFullPath(pluginDir);
            var assemblyPath = Path.GetFullPath(Path.Combine(pluginDir, manifest.Assembly));
            if (!assemblyPath.StartsWith(pluginDirFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assemblyPath, pluginDirFull, StringComparison.OrdinalIgnoreCase))
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, "Plugin assembly resolved outside plugin directory.");
            }

            if (!File.Exists(assemblyPath))
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, "Plugin assembly not found.");
            }

            loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(manifest.Type, throwOnError: false, ignoreCase: false);
            if (type is null)
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, "Plugin type not found in assembly.");
            }

            if (!typeof(IPlugin).IsAssignableFrom(type))
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, "Plugin type does not implement IPlugin.");
            }

            plugin = Activator.CreateInstance(type) as IPlugin;
            if (plugin is null)
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, "Failed to create plugin instance.");
            }

            if (plugin is IPluginIdentity identity &&
                !string.IsNullOrWhiteSpace(identity.Id) &&
                !string.Equals(identity.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
            {
                return new PluginHealthCheckResult(pluginId, pluginDir, false, "Plugin identity mismatch.");
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

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutPerPlugin);

            var token = cts.Token;
            _logger.LogInformation("HealthCheck: QueryAsync start. Id={PluginId} Dir={Dir}", manifest.Id, pluginDir);
            var queryResults = await plugin.QueryAsync(string.Empty, token);
            _logger.LogInformation("HealthCheck: QueryAsync ok. Id={PluginId} Results={ResultCount}", manifest.Id, queryResults?.Count ?? 0);

            if (plugin is IPluginSelfTest selfTest)
            {
                _logger.LogInformation("HealthCheck: SelfTestAsync start. Id={PluginId}", manifest.Id);
                await selfTest.SelfTestAsync(token);
                _logger.LogInformation("HealthCheck: SelfTestAsync ok. Id={PluginId}", manifest.Id);
            }

            return new PluginHealthCheckResult(manifest.Id, pluginDir, true, null);
        }
        catch (OperationCanceledException ex)
        {
            var message = cancellationToken.IsCancellationRequested
                ? "Cancelled."
                : "Timeout.";

            _logger.LogWarning(ex, "HealthCheck: cancelled/timeout. Id={PluginId} Dir={Dir}", pluginId, pluginDir);
            return new PluginHealthCheckResult(pluginId, pluginDir, false, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HealthCheck: failed. Id={PluginId} Dir={Dir}", pluginId, pluginDir);
            return new PluginHealthCheckResult(pluginId, pluginDir, false, ex.Message);
        }
        finally
        {
            if (plugin is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }

            plugin = null;

            if (loadContext is not null)
            {
                var weakRef = new WeakReference(loadContext);
                try { loadContext.Unload(); } catch { }
                loadContext = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (weakRef.IsAlive)
                {
                    _logger.LogDebug("HealthCheck: load context still alive after unload. Id={PluginId} Dir={Dir}", pluginId, pluginDir);
                }
            }
        }
    }

    private static bool ValidateManifest(ExternalPluginManifest manifest, out string error)
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

        return true;
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
