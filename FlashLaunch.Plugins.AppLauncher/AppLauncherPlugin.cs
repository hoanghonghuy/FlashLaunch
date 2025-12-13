using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;
using FlashLaunch.Core.Utilities;

namespace FlashLaunch.Plugins.AppLauncher;

public sealed class AppLauncherPlugin : IPlugin, IPluginIdentity
{
    private readonly IStringLocalizer _localizer;
    private readonly IAppIndexPathProvider _pathProvider;
    private Lazy<IReadOnlyList<AppEntry>> _index = null!;
    private readonly ConcurrentDictionary<string, int> _usageCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _usageFilePath;
    private readonly object _usageLock = new();

    public AppLauncherPlugin(IStringLocalizer localizer, IAppIndexPathProvider pathProvider)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _usageFilePath = AppDataPaths.UsageCachePath;
        InitializeIndex();
        LoadUsageCounts();

        // Pre-warm the index in the background so the first query feels responsive.
        _ = Task.Run(() =>
        {
            var _ = _index.Value;
        });
    }

    public string Name => _localizer["Plugin_Applications_Name"];

    public string Description => _localizer["Plugin_Applications_Description"];

    public string Id => "flashlaunch.builtin.app_launcher";

    public PluginKind Kind => PluginKind.Application;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery) || searchQuery.Length < 2)
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var entries = _index.Value;
        var scored = entries
            .Select(entry =>
            {
                var nameScore = FuzzyMatcher.CalculateScore(entry.DisplayName, searchQuery);

                var fileName = Path.GetFileNameWithoutExtension(entry.LaunchPath) ?? string.Empty;
                var fileScore = FuzzyMatcher.CalculateScore(fileName, searchQuery);

                var baseScore = Math.Max(nameScore, fileScore);
                var usageBoost = Math.Min(0.25, GetUsageBoost(entry.LaunchPath));
                var finalScore = Math.Clamp(baseScore + usageBoost, 0, 1);
                var isMostUsed = usageBoost >= 0.15;
                return new { entry, finalScore, isMostUsed };
            })
            .ToList();

        // Lọc chính: ưu tiên các kết quả có điểm đủ tốt.
        var filtered = scored
            .Where(x => x.finalScore >= 0.35)
            .OrderByDescending(x => x.finalScore)
            .ThenBy(x => x.entry.DisplayName)
            .Take(10)
            .ToList();

        // Fallback: nếu không có kết quả vượt ngưỡng, vẫn trả về một vài ứng dụng khớp tốt nhất.
        if (filtered.Count == 0)
        {
            filtered = scored
                .Where(x => x.finalScore > 0)
                .OrderByDescending(x => x.finalScore)
                .ThenBy(x => x.entry.DisplayName)
                .Take(5)
                .ToList();
        }

        var results = filtered
            .Select(x =>
            {
                var pathDisplay = x.entry.TargetPath ?? x.entry.ShortcutPath;
                var subtitle = x.isMostUsed
                    ? _localizer.Format("Plugin_Applications_MostUsedSubtitle", pathDisplay)
                    : pathDisplay;
                return new SearchResult
                {
                    Title = x.entry.DisplayName,
                    Subtitle = subtitle,
                    Score = x.finalScore,
                    IconPath = x.entry.IconPath,
                    Payload = x.entry.LaunchPath,
                    Plugin = this
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    public Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default)
    {
        if (result.Payload is not string path || string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Invalid launch path.");
        }

        Launch(path);

        _usageCounts.AddOrUpdate(path, 1, static (_, current) => Math.Min(current + 1, 50));
        SaveUsageCounts();
        return Task.CompletedTask;
    }

    private static void Launch(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Executable not found.", path);
        }

        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true
        };

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start process.");
        }
    }

    private double GetUsageBoost(string launchPath)
    {
        if (_usageCounts.TryGetValue(launchPath, out var uses))
        {
            return Math.Min(0.25, uses * 0.02);
        }

        return 0;
    }

    public void RefreshIndex() => InitializeIndex();

    public void ResetUsageData()
    {
        _usageCounts.Clear();
        try
        {
            if (File.Exists(_usageFilePath))
            {
                File.Delete(_usageFilePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    private void InitializeIndex()
    {
        _index = new Lazy<IReadOnlyList<AppEntry>>(BuildIndex, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private IReadOnlyList<AppEntry> BuildIndex()
    {
        var entries = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in EnumerateCandidateDirectories().Where(Directory.Exists))
        {
            try
            {
                foreach (var shortcut in Directory.EnumerateFiles(directory, "*.lnk", SearchOption.AllDirectories))
                {
                    AddEntry(entries, shortcut);
                }
            }
            catch
            {
                // Ignore folders we cannot read.
            }
        }

        return entries.Values.ToList();
    }

    private IEnumerable<string> EnumerateCandidateDirectories()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        if (_pathProvider is not null)
        {
            foreach (var path in _pathProvider.GetAdditionalDirectories())
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    yield return path;
                }
            }
        }
    }

    private static void AddEntry(IDictionary<string, AppEntry> entries, string shortcutPath)
    {
        var displayName = Path.GetFileNameWithoutExtension(shortcutPath);

        ShortcutResolver.ShortcutInfo? info = null;
        if (ShortcutResolver.TryResolve(shortcutPath, out var resolved))
        {
            info = resolved;
            if (!string.IsNullOrWhiteSpace(resolved.DisplayName))
            {
                displayName = resolved.DisplayName;
            }
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        displayName = Deduplicate(entries, displayName);

        var targetPath = info?.TargetPath;
        var launchPath = File.Exists(targetPath) ? targetPath! : shortcutPath;
        var iconPath = NormalizeIconPath(info?.IconPath, targetPath, shortcutPath);

        entries[displayName] = new AppEntry(displayName, targetPath, shortcutPath, launchPath, iconPath);
    }

    private static string? NormalizeIconPath(string? iconPath, string? targetPath, string shortcutPath)
    {
        string? candidate;

        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            candidate = iconPath;
        }
        else if (!string.IsNullOrWhiteSpace(targetPath))
        {
            candidate = targetPath;
        }
        else
        {
            candidate = shortcutPath;
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (!File.Exists(candidate))
        {
            return null;
        }

        var ext = Path.GetExtension(candidate);
        if (ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        return null;
    }

    private static string Deduplicate(IDictionary<string, AppEntry> entries, string baseName)
    {
        var candidate = baseName;
        var suffix = 2;
        while (entries.ContainsKey(candidate))
        {
            candidate = $"{baseName} ({suffix++})";
        }

        return candidate;
    }

    private void LoadUsageCounts()
    {
        try
        {
            if (!File.Exists(_usageFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_usageFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (data is null)
            {
                return;
            }

            foreach (var pair in data)
            {
                _usageCounts[pair.Key] = pair.Value;
            }
        }
        catch
        {
            // ignore corrupt usage cache
        }
    }

    private void SaveUsageCounts()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_usageFilePath)!);
            var snapshot = _usageCounts.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            lock (_usageLock)
            {
                File.WriteAllText(_usageFilePath, json);
            }
        }
        catch
        {
            // ignore write failures
        }
    }

    private sealed record AppEntry(
        string DisplayName,
        string? TargetPath,
        string ShortcutPath,
        string LaunchPath,
        string? IconPath);
}
