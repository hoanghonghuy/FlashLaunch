using System;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.UI.Localization;

namespace FlashLaunch.UI.Services;

public sealed class ResourceDictionaryLocalizer : IStringLocalizer
{
    public string this[string key] => LocalizationManager.GetString(key);

    public string Format(string key, params object[] args)
    {
        var template = LocalizationManager.GetString(key);
        return string.Format(template, args);
    }
}
