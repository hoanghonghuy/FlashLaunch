using System;
using System.Linq;
using System.Windows;

namespace FlashLaunch.UI.Localization;

public static class LocalizationManager
{
    private const string DefaultLanguage = "en";

    public static string CurrentLanguage { get; private set; } = DefaultLanguage;

    public static void ApplyLanguage(string? languageCode)
    {
        languageCode = string.IsNullOrWhiteSpace(languageCode) ? DefaultLanguage : languageCode;

        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var newDict = new ResourceDictionary
        {
            Source = new Uri($"/FlashLaunch.UI;component/Localization/Strings.{languageCode}.xaml", UriKind.Relative)
        };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Localization/Strings."));

        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Add(newDict);
        CurrentLanguage = languageCode;
    }

    public static string GetString(string key)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return key;
        }

        if (app.TryFindResource(key) is string value)
        {
            return value;
        }

        return key;
    }
}
