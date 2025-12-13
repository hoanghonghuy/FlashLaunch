using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.Core.Models;

namespace FlashLaunch.Plugins.Calculator;

public sealed class CalculatorPlugin : IPlugin, IPluginIdentity
{
    private readonly IStringLocalizer _localizer;

    public CalculatorPlugin(IStringLocalizer localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    public string Name => _localizer["Plugin_Calculator_Name"];

    public string Description => _localizer["Plugin_Calculator_Description"];

    public string Id => "flashlaunch.builtin.calculator";

    public PluginKind Kind => PluginKind.Calculator;

    public Task<IReadOnlyList<SearchResult>> QueryAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        if (!LooksLikeExpression(searchQuery))
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var result = Evaluate(searchQuery);

        if (result is null)
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
        }

        var searchResult = new SearchResult
        {
            Title = result,
            Subtitle = searchQuery,
            Score = 1.0,
            Payload = result,
            Plugin = this
        };

        return Task.FromResult<IReadOnlyList<SearchResult>>(new[] { searchResult });
    }

    public Task ExecuteAsync(SearchResult result, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static bool LooksLikeExpression(string text)
    {
        foreach (var c in text)
        {
            if (char.IsDigit(c) || "+-*/().,^%".Contains(c, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Evaluate(string expression)
    {
        try
        {
            var dataTable = new DataTable();
            dataTable.Locale = CultureInfo.InvariantCulture;
            var result = dataTable.Compute(expression, null);
            return Convert.ToDouble(result, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}
