using System;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.UI.Configuration;

namespace FlashLaunch.UI.Services;

public sealed class SystemCommandState : ISystemCommandState
{
    private readonly AppConfig _config;

    public SystemCommandState(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool IsCommandEnabled(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var states = _config.SystemCommands;
        if (states is null || states.Count == 0)
        {
            return true;
        }

        if (states.TryGetValue(key, out var enabled))
        {
            return enabled;
        }

        return true;
    }
}
