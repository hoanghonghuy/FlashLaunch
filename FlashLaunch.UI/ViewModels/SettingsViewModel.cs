using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using FlashLaunch.Core.Abstractions;
using FlashLaunch.UI.Configuration;
using FlashLaunch.UI.Localization;
using FlashLaunch.UI.Services;
using FlashLaunch.UI.Services.Plugins;
using FlashLaunch.UI.Theming;

namespace FlashLaunch.UI.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly IPluginCatalog _pluginCatalog;
    private readonly StringComparer _pathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly ShellController _shellController;
    private readonly IPluginStateProvider _pluginStateProvider;
    private readonly MainViewModel _mainViewModel;
    private readonly MaintenanceService _maintenanceService;
    private readonly PluginHealthCheckService _pluginHealthCheckService;

    public SettingsViewModel(
        AppConfig config,
        ConfigService configService,
        IPluginCatalog pluginCatalog,
        ShellController shellController,
        IPluginStateProvider pluginStateProvider,
        MainViewModel mainViewModel,
        MaintenanceService maintenanceService,
        PluginHealthCheckService pluginHealthCheckService)
    {
        _config = config;
        _configService = configService;
        _pluginCatalog = pluginCatalog;
        _shellController = shellController;
        _pluginStateProvider = pluginStateProvider;
        _mainViewModel = mainViewModel;
        _maintenanceService = maintenanceService;
        _pluginHealthCheckService = pluginHealthCheckService;

        OriginalHotkey = config.Hotkey?.Trim() ?? string.Empty;
        Hotkey = OriginalHotkey;

        Language = string.IsNullOrWhiteSpace(config.Language) ? "vi" : config.Language;

        Theme = string.IsNullOrWhiteSpace(config.Theme) ? "System" : config.Theme;

        PluginToggles = new ObservableCollection<PluginToggleViewModel>();
        RebuildPluginToggles(preserveCurrentSelections: false);

        CustomDirectories = new ObservableCollection<string>(config.CustomAppDirectories ?? new List<string>());

        WebSearchProviders = new ObservableCollection<WebSearchProviderToggleViewModel>(
            BuildWebSearchProviderToggles(config));

        SystemCommandToggles = new ObservableCollection<SystemCommandToggleViewModel>(
            BuildSystemCommandToggles(config));

        ShowResultIcons = config.ShowResultIcons;
        ShowPluginBadges = config.ShowPluginBadges;
        _persistentIconCacheEnabled = config.PersistentIconCacheEnabled;
    }

    private string _hotkey = string.Empty;
    private string? _validationMessage;
    private bool _isValidationError;
    private string _newDirectoryPath = string.Empty;
    private string _language = "vi";
    private string _theme = "System";
    private bool _showResultIcons = true;
    private bool _showPluginBadges = true;
    private bool _persistentIconCacheEnabled = true;

    public string OriginalHotkey { get; private set; } = string.Empty;

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    public ObservableCollection<PluginToggleViewModel> PluginToggles { get; }

    public ObservableCollection<WebSearchProviderToggleViewModel> WebSearchProviders { get; }

    public ObservableCollection<SystemCommandToggleViewModel> SystemCommandToggles { get; }

    public ObservableCollection<string> CustomDirectories { get; }

    public string NewDirectoryPath
    {
        get => _newDirectoryPath;
        set => SetProperty(ref _newDirectoryPath, value);
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public bool ShowResultIcons
    {
        get => _showResultIcons;
        set => SetProperty(ref _showResultIcons, value);
    }

    public bool ShowPluginBadges
    {
        get => _showPluginBadges;
        set => SetProperty(ref _showPluginBadges, value);
    }

    public bool PersistentIconCacheEnabled
    {
        get => _persistentIconCacheEnabled;
        set => SetProperty(ref _persistentIconCacheEnabled, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool IsValidationError
    {
        get => _isValidationError;
        set => SetProperty(ref _isValidationError, value);
    }

    public bool ApplySettings()
    {
        var trimmedHotkey = (Hotkey ?? string.Empty).Trim();
        var hotkeyChanged = !string.Equals(
            trimmedHotkey,
            OriginalHotkey,
            StringComparison.OrdinalIgnoreCase);

        if (hotkeyChanged)
        {
            if (!HotkeyParser.TryParse(trimmedHotkey, out var modifiers, out var key))
            {
                ValidationMessage = LocalizationManager.GetString("Settings_Hotkey_Invalid");
                IsValidationError = true;
                return false;
            }

            if (!_shellController.TryUpdateHotkey(modifiers, key, out var error))
            {
                ValidationMessage = error ?? LocalizationManager.GetString("Settings_Hotkey_UpdateFailed");
                IsValidationError = true;
                return false;
            }
        }

        foreach (var toggle in PluginToggles)
        {
            _pluginStateProvider.UpdateState(toggle.PluginId, toggle.IsEnabled);
        }

        Save();
        LocalizationManager.ApplyLanguage(Language);
        ThemeManager.ApplyTheme(Theme);
        ValidationMessage = null;
        IsValidationError = false;
        _mainViewModel.RefreshResults();
        _mainViewModel.NotifySettingsApplied();
        return true;
    }

    public void RefreshIndex()
    {
        _maintenanceService.RefreshAppIndex();
        _mainViewModel.RefreshResults();
        _mainViewModel.NotifyStatus(LocalizationManager.GetString("Settings_AppIndex_Refreshed"));
    }

    public void ResetUsageLogs()
    {
        if (_maintenanceService.TryResetUsageAndLogs(out var error))
        {
            _mainViewModel.NotifyStatus(LocalizationManager.GetString("Settings_ResetUsageLogs_Success"));
            ValidationMessage = null;
            IsValidationError = false;
        }
        else
        {
            ValidationMessage = error ?? LocalizationManager.GetString("Settings_ResetUsageLogs_Failed");
            IsValidationError = true;
        }
    }

    public void ReloadPlugins()
    {
        _mainViewModel.ClearResults();
        _pluginCatalog.Reload();
        RebuildPluginToggles(preserveCurrentSelections: true);
        ValidationMessage = LocalizationManager.GetString("Settings_Plugins_Reloaded");
        IsValidationError = false;
        _mainViewModel.RefreshResults();
    }

    public async System.Threading.Tasks.Task HealthCheckPluginsAsync()
    {
        try
        {
            var summary = await _pluginHealthCheckService.RunExternalPluginsAsync(TimeSpan.FromSeconds(2));

            if (summary.Total == 0)
            {
                ValidationMessage = LocalizationManager.GetString("Settings_Plugins_HealthCheck_NoPlugins");
                IsValidationError = false;
                return;
            }

            if (summary.Failed > 0)
            {
                ValidationMessage = string.Format(
                    LocalizationManager.GetString("Settings_Plugins_HealthCheck_FailedSummary"),
                    summary.Passed,
                    summary.Failed);
                IsValidationError = true;
            }
            else
            {
                ValidationMessage = string.Format(
                    LocalizationManager.GetString("Settings_Plugins_HealthCheck_PassedSummary"),
                    summary.Passed);
                IsValidationError = false;
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
            IsValidationError = true;
        }
    }

    private void RebuildPluginToggles(bool preserveCurrentSelections)
    {
        var current = preserveCurrentSelections
            ? PluginToggles.ToDictionary(p => p.PluginId, p => p.IsEnabled, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        PluginToggles.Clear();
        var plugins = _pluginCatalog.GetPlugins();
        foreach (var plugin in plugins)
        {
            var pluginId = plugin is IPluginIdentity identity && !string.IsNullOrWhiteSpace(identity.Id)
                ? identity.Id
                : plugin.Name;

            var enabled = current.TryGetValue(pluginId, out var selected)
                ? selected
                : _pluginStateProvider.IsEnabled(pluginId, plugin.Name);

            PluginToggles.Add(new PluginToggleViewModel(pluginId, plugin.Name, enabled));
        }
    }

    public bool TryAddCustomDirectory(string? path, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = LocalizationManager.GetString("Settings_CustomDir_EmptyInput");
            return false;
        }

        var normalized = path.Trim();
        if (!Directory.Exists(normalized))
        {
            errorMessage = LocalizationManager.GetString("Settings_CustomDir_NotExists");
            return false;
        }

        if (CustomDirectories.Any(d => _pathComparer.Equals(d, normalized)))
        {
            errorMessage = LocalizationManager.GetString("Settings_CustomDir_Duplicate");
            return false;
        }

        CustomDirectories.Add(normalized);
        errorMessage = null;
        return true;
    }

    public void RemoveCustomDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var match = CustomDirectories.FirstOrDefault(d => _pathComparer.Equals(d, path));
        if (match is not null)
        {
            CustomDirectories.Remove(match);
        }
    }

    public void Save()
    {
        var trimmedHotkey = Hotkey?.Trim() ?? string.Empty;
        _config.Hotkey = trimmedHotkey;
        OriginalHotkey = trimmedHotkey;

        foreach (var toggle in PluginToggles)
        {
            _config.PluginStates[toggle.PluginId] = toggle.IsEnabled;
        }

        _config.WebSearchProviders = WebSearchProviders
            .ToDictionary(p => p.Prefix, p => p.IsEnabled, StringComparer.OrdinalIgnoreCase);

        _config.SystemCommands = SystemCommandToggles
            .ToDictionary(c => c.Key, c => c.IsEnabled, StringComparer.OrdinalIgnoreCase);

        _config.CustomAppDirectories = CustomDirectories.ToList();

        _config.Language = Language;

        _config.Theme = Theme;

        _config.ShowResultIcons = ShowResultIcons;

        _config.ShowPluginBadges = ShowPluginBadges;

        _config.PersistentIconCacheEnabled = PersistentIconCacheEnabled;

        _configService.Save(_config);
    }

    public void ClearIconCache()
    {
        if (_maintenanceService.TryClearIconCache(out var error))
        {
            ValidationMessage = LocalizationManager.GetString("Settings_IconCache_Cleared");
            IsValidationError = false;
        }
        else
        {
            ValidationMessage = error ?? LocalizationManager.GetString("Settings_IconCache_ClearFailed");
            IsValidationError = true;
        }
    }

    private IEnumerable<WebSearchProviderToggleViewModel> BuildWebSearchProviderToggles(AppConfig config)
    {
        var states = config.WebSearchProviders ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        yield return CreateWebProviderToggle("g", "Settings_WebSearch_Google", states);
        yield return CreateWebProviderToggle("yt", "Settings_WebSearch_YouTube", states);
        yield return CreateWebProviderToggle("ddg", "Settings_WebSearch_DuckDuckGo", states);
    }

    private static WebSearchProviderToggleViewModel CreateWebProviderToggle(
        string prefix,
        string resourceKey,
        IDictionary<string, bool> states)
    {
        var displayName = LocalizationManager.GetString(resourceKey);
        var isEnabled = !states.TryGetValue(prefix, out var value) || value;
        return new WebSearchProviderToggleViewModel(prefix, displayName, isEnabled);
    }

    private IEnumerable<SystemCommandToggleViewModel> BuildSystemCommandToggles(AppConfig config)
    {
        var states = config.SystemCommands ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        yield return CreateSystemCommandToggle("shutdown", "> shutdown", "Plugin_System_Shutdown_Title", states);
        yield return CreateSystemCommandToggle("restart", "> restart", "Plugin_System_Restart_Title", states);
        yield return CreateSystemCommandToggle("sleep", "> sleep", "Plugin_System_Sleep_Title", states);
        yield return CreateSystemCommandToggle("lock", "> lock", "Plugin_System_Lock_Title", states);
        yield return CreateSystemCommandToggle("empty trash", "> empty trash", "Plugin_System_EmptyRecycleBin_Title", states);
    }

    private static SystemCommandToggleViewModel CreateSystemCommandToggle(
        string key,
        string triggerText,
        string titleResourceKey,
        IDictionary<string, bool> states)
    {
        var title = LocalizationManager.GetString(titleResourceKey);
        var displayName = $"{triggerText}  {title}";
        var isEnabled = !states.TryGetValue(key, out var value) || value;
        return new SystemCommandToggleViewModel(key, displayName, isEnabled);
    }
}

public sealed class PluginToggleViewModel : ObservableObject
{
    private bool _isEnabled;

    public PluginToggleViewModel(string pluginId, string name, bool isEnabled)
    {
        PluginId = pluginId;
        Name = name;
        _isEnabled = isEnabled;
    }

    public string PluginId { get; }

    public string Name { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public sealed class WebSearchProviderToggleViewModel : ObservableObject
{
    private bool _isEnabled;

    public WebSearchProviderToggleViewModel(string prefix, string displayName, bool isEnabled)
    {
        Prefix = prefix;
        DisplayName = displayName;
        _isEnabled = isEnabled;
    }

    public string Prefix { get; }

    public string DisplayName { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public sealed class SystemCommandToggleViewModel : ObservableObject
{
    private bool _isEnabled;

    public SystemCommandToggleViewModel(string key, string displayName, bool isEnabled)
    {
        Key = key;
        DisplayName = displayName;
        _isEnabled = isEnabled;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}