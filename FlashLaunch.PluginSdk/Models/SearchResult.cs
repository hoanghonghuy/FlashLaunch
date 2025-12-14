using FlashLaunch.Core.Abstractions;

namespace FlashLaunch.Core.Models;

public sealed class SearchResult
{
    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public double Score { get; init; } = 0;

    public string? IconPath { get; init; }

    public object? Payload { get; init; }

    public required IPlugin Plugin { get; init; }

    public string PluginName => Plugin.Name;
}
