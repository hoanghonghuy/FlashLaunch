using System.Collections.Generic;

namespace FlashLaunch.UI.Configuration;

public sealed class AppConfig
{
    public string Hotkey { get; set; } = "Alt + Space";

    public Dictionary<string, bool> PluginStates { get; set; } = new();

    public Dictionary<string, bool> WebSearchProviders { get; set; } = new();

    public Dictionary<string, bool> SystemCommands { get; set; } = new();

    public List<string> CustomAppDirectories { get; set; } = new();

    public string Language { get; set; } = "vi";

    public string Theme { get; set; } = "System";

    public bool ShowResultIcons { get; set; } = true;

    public bool ShowPluginBadges { get; set; } = true;

    public bool PersistentIconCacheEnabled { get; set; } = true;
}
