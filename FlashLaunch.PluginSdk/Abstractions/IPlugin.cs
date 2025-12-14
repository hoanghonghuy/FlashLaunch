using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Models;

namespace FlashLaunch.Core.Abstractions;

public interface IPlugin
{
    string Name { get; }

    string Description { get; }

    PluginKind Kind { get; }

    Task<IReadOnlyList<SearchResult>> QueryAsync(string searchQuery, CancellationToken cancellationToken = default);

    Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default);
}
