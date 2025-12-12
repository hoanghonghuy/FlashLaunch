using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using FlashLaunch.UI.Configuration;
using FlashLaunch.UI.Interop;
using FlashLaunch.UI.Localization;
using FlashLaunch.UI.ViewModels;

namespace FlashLaunch.UI.Services;

public sealed class ShellController : IDisposable
{
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly IHotkeyService _hotkeyService;
    private readonly AppConfig _config;
    private bool _initialized;
    private int _toggleHotkeyId;
    private bool _disposed;
    private HotkeyModifiers _currentModifiers = HotkeyModifiers.Alt;
    private Key _currentKey = Key.Space;

    public ShellController(MainWindow window, MainViewModel viewModel, IHotkeyService hotkeyService, AppConfig config)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        _hotkeyService.Initialize(_window);
        if (!HotkeyParser.TryParse(_config.Hotkey, out var modifiers, out var key))
        {
            modifiers = HotkeyModifiers.Alt;
            key = Key.Space;
        }

        if (!RegisterHotkey(modifiers, key, out _))
        {
            RegisterHotkey(HotkeyModifiers.Alt, Key.Space, out _);
        }

        _window.PreviewKeyDown += OnWindowPreviewKeyDown;
        _window.Deactivated += OnWindowDeactivated;

        HideShell();
    }

    public bool TryUpdateHotkey(HotkeyModifiers modifiers, Key key, out string? errorMessage)
    {
        var success = RegisterHotkey(modifiers, key, out errorMessage);
        if (success)
        {
            _config.Hotkey = HotkeyParser.Normalize($"{GetModifierString(modifiers)}{key}");
        }

        return success;
    }

    private static string GetModifierString(HotkeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl + ");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift + ");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt + ");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win + ");
        return string.Concat(parts);
    }

    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private bool RegisterHotkey(HotkeyModifiers modifiers, Key key, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var newId = _hotkeyService.RegisterHotkey(modifiers, key, ToggleVisibility);

            if (_toggleHotkeyId != 0)
            {
                _hotkeyService.UnregisterHotkey(_toggleHotkeyId);
            }

            _toggleHotkeyId = newId;
            _currentModifiers = modifiers;
            _currentKey = key;
            return true;
        }
        catch (Win32Exception ex)
        {
            errorMessage = ex.NativeErrorCode == ErrorHotkeyAlreadyRegistered
                ? LocalizationManager.GetString("Settings_Hotkey_InUse")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationManager.GetString("Settings_Hotkey_Win32Error"),
                    ex.NativeErrorCode);
            return false;
        }
    }

    private void ToggleVisibility()
    {
        if (_window.Dispatcher.CheckAccess())
        {
            if (_window.IsVisible)
            {
                HideShell();
            }
            else
            {
                ShowShell();
            }
        }
        else
        {
            _window.Dispatcher.Invoke(ToggleVisibility);
        }
    }

    private void ShowShell()
    {
        _window.Dispatcher.Invoke(() =>
        {
            if (!_window.IsVisible)
            {
                _window.Show();
            }

            _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.FocusSearchBox();
        });
    }

    private void HideShell()
    {
        _window.Dispatcher.Invoke(() =>
        {
            if (_window.IsVisible)
            {
                _window.Hide();
            }
        });
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.QueryText))
            {
                _viewModel.QueryText = string.Empty;
                e.Handled = true;
                return;
            }

            HideShell();
            e.Handled = true;
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_window.IsVisible)
        {
            HideShell();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
        _window.Deactivated -= OnWindowDeactivated;

        if (_toggleHotkeyId != 0)
        {
            _hotkeyService.UnregisterHotkey(_toggleHotkeyId);
            _toggleHotkeyId = 0;
        }

        _hotkeyService.Dispose();
    }
}
