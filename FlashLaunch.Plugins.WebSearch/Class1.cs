using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;

namespace FlashLaunch.Plugins.WebSearch;

public sealed class WebSearchPlugin : IPlugin
{
    private static readonly IReadOnlyDictionary<string, string> SearchProviders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = "https://www.google.com/search?q={0}",
        ["yt"] = "https://www.youtube.com/results?search_query={0}",
        ["ddg"] = "https://duckduckgo.com/?q={0}"
    };

    private readonly IStringLocalizer _localizer;
    private readonly IWebSearchProviderState _providerState;

    public WebSearchPlugin(IStringLocalizer localizer, IWebSearchProviderState providerState)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _providerState = providerState ?? throw new ArgumentNullException(nameof(providerState));
    }

    public string Name => _localizer["Plugin_Web_Name"];

    public string Description => _localizer["Plugin_Web_Description"];

    public PluginKind Kind => PluginKind.Web;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var trimmed = searchQuery.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace <= 0)
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var prefix = trimmed[..firstSpace];
        var payload = trimmed[(firstSpace + 1)..].Trim();

        if (string.IsNullOrEmpty(payload) || !SearchProviders.ContainsKey(prefix))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        if (!_providerState.IsProviderEnabled(prefix))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var providerName = ResolveProviderName(prefix);
        var title = _localizer.Format("Plugin_Web_SearchTitle", providerName, payload);
        var result = new SearchResult
        {
            Title = title,
            Subtitle = _localizer.Format("Plugin_Web_Subtitle", prefix),
            Score = 0.85,
            Payload = new WebPayload(prefix, payload),
            Plugin = this
        };

        return Task.FromResult<IReadOnlyList<SearchResult>>(new[] { result });
    }

    public Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default)
    {
        if (result.Payload is not WebPayload payload || !SearchProviders.TryGetValue(payload.Prefix, out var template))
        {
            throw new InvalidOperationException("Invalid web search payload.");
        }

        var encoded = Uri.EscapeDataString(payload.Query);
        var url = string.Format(template, encoded);
        var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        if (process is null)
        {
            throw new InvalidOperationException("Failed to launch web browser.");
        }

        return Task.CompletedTask;
    }

    private string ResolveProviderName(string prefix) => prefix.ToLowerInvariant() switch
    {
        "g" => _localizer["Plugin_Web_Provider_Google"],
        "yt" => _localizer["Plugin_Web_Provider_YouTube"],
        "ddg" => _localizer["Plugin_Web_Provider_DuckDuckGo"],
        _ => _localizer["Plugin_Web_Name"]
    };

    private sealed record WebPayload(string Prefix, string Query);
}
