using System;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.UI.Configuration;

namespace FlashLaunch.UI.Services;

public sealed class WebSearchProviderState : IWebSearchProviderState
{
    private readonly AppConfig _config;

    public WebSearchProviderState(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool IsProviderEnabled(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        var states = _config.WebSearchProviders;
        if (states is null || states.Count == 0)
        {
            return true;
        }

        if (states.TryGetValue(prefix, out var enabled))
        {
            return enabled;
        }

        return true;
    }
}
