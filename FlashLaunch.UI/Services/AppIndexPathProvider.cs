using System.Collections.Generic;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.UI.Configuration;

namespace FlashLaunch.UI.Services;

public sealed class AppIndexPathProvider : IAppIndexPathProvider
{
    private readonly AppConfig _config;

    public AppIndexPathProvider(AppConfig config)
    {
        _config = config;
    }

    public IEnumerable<string> GetAdditionalDirectories()
    {
        return _config.CustomAppDirectories ?? new List<string>();
    }
}
