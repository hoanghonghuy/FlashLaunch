using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;

namespace FlashLaunch.SamplePlugins.HelloWorld;

public sealed class HelloWorldPlugin : IPlugin, IPluginIdentity
{
    public string Id => "sample.hello_world";

    public string Name => "Hello World";

    public string Description => "Sample third-party plugin for FlashLaunch";

    public PluginKind Kind => PluginKind.Utility;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var trimmed = searchQuery.Trim();
        if (!trimmed.StartsWith("hello", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var result = new SearchResult
        {
            Title = "Hello from external plugin!",
            Subtitle = "Press Enter to open FlashLaunch repository",
            Score = 0.9,
            Payload = "https://github.com/hoanghonghuy/FlashLaunch",
            Plugin = this
        };

        return Task.FromResult<IReadOnlyList<SearchResult>>(new[] { result });
    }

    public Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default)
    {
        if (result.Payload is not string url || string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
