using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;
using FlashLaunch.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FlashLaunch.Core.Services;

public interface IQueryDispatcher
{
    Task<IReadOnlyList<SearchResult>> DispatchAsync(string query, CancellationToken cancellationToken = default);
}

public sealed class QueryDispatcher : IQueryDispatcher
{
    private readonly IEnumerable<IPlugin> _plugins;
    private readonly ILogger<QueryDispatcher> _logger;
    private readonly IPluginStateProvider _stateProvider;

    public QueryDispatcher(IEnumerable<IPlugin> plugins, ILogger<QueryDispatcher> logger, IPluginStateProvider stateProvider)
    {
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
    }

    public async Task<IReadOnlyList<SearchResult>> DispatchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResult>();
        }

        var stopwatch = ValueStopwatch.StartNew();
        var activePlugins = _plugins.Where(p => _stateProvider.IsEnabled(p.Name)).ToList();
        var tasks = activePlugins.Select(plugin => QueryPluginSafeAsync(plugin, query, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var elapsed = stopwatch.GetElapsedTime();

        _logger.LogDebug("Query \"{Query}\" took {ElapsedMs}ms across {PluginCount} plugins.",
            query,
            elapsed.TotalMilliseconds,
            activePlugins.Count);

        return results
            .SelectMany(static r => r)
            .OrderByDescending(result => result.Score)
            .ToList();
    }

    private async Task<IReadOnlyList<SearchResult>> QueryPluginSafeAsync(IPlugin plugin, string query, CancellationToken cancellationToken)
    {
        try
        {
            var results = await plugin.QueryAsync(query, cancellationToken).ConfigureAwait(false);
            var safeResults = results ?? Array.Empty<SearchResult>();

            _logger.LogDebug(
                "Plugin {PluginName} returned {ResultCount} results for query \"{Query}\".",
                plugin.Name,
                safeResults.Count,
                query);

            return safeResults;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {PluginName} failed to process query {Query}.", plugin.Name, query);
            return Array.Empty<SearchResult>();
        }
    }
}
