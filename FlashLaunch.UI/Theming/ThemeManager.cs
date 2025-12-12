using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace FlashLaunch.UI.Theming;

public static class ThemeManager
{
    private const string DefaultTheme = "System";

    public static string CurrentTheme { get; private set; } = DefaultTheme;

    public static string ActualTheme { get; private set; } = "Dark";

    public static event EventHandler? ThemeChanged;

    public static void ApplyTheme(string? themeMode)
    {
        themeMode = string.IsNullOrWhiteSpace(themeMode) ? DefaultTheme : themeMode;

        var actual = ResolveActualTheme(themeMode);

        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var newDict = new ResourceDictionary
        {
            Source = new Uri($"/FlashLaunch.UI;component/Themes/Theme.{actual}.xaml", UriKind.Relative)
        };

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Themes/Theme."));

        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Add(newDict);

        CurrentTheme = themeMode;
        ActualTheme = actual;

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string ResolveActualTheme(string themeMode)
    {
        switch (themeMode)
        {
            case "Light":
                return "Light";
            case "Dark":
                return "Dark";
            case "System":
            default:
                return IsSystemLightTheme() ? "Light" : "Dark";
        }
    }

    private static bool IsSystemLightTheme()
    {
        const string keyPath = @"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        const string valueName = "AppsUseLightTheme";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            if (key?.GetValue(valueName) is int value)
            {
                return value > 0;
            }
        }
        catch
        {
            // ignore and fall back to dark
        }

        return false;
    }
}
