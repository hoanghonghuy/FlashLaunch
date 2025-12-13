using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly IHotkeyService _hotkeyService;
    private readonly AppConfig _config;
    private MainWindow? _window;
    private MainViewModel? _viewModel;
    private bool _initialized;
    private int _toggleHotkeyId;
    private bool _disposed;
    private HotkeyModifiers _currentModifiers = HotkeyModifiers.Alt;
    private Key _currentKey = Key.Space;

    public ShellController(IServiceProvider serviceProvider, IHotkeyService hotkeyService, AppConfig config)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

        _hotkeyService.Initialize();
        if (!HotkeyParser.TryParse(_config.Hotkey, out var modifiers, out var key))
        {
            modifiers = HotkeyModifiers.Alt;
            key = Key.Space;
        }

        if (!RegisterHotkey(modifiers, key, out _))
        {
            RegisterHotkey(HotkeyModifiers.Alt, Key.Space, out _);
        }
    }

    public void ShowMainWindow()
    {
        EnsureWindowCreated();
        ShowShell();
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
        EnsureWindowCreated();
        if (_window!.Dispatcher.CheckAccess())
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
            _window!.Dispatcher.Invoke(ToggleVisibility);
        }
    }

    private void EnsureWindowCreated()
    {
        if (_window is not null)
        {
            return;
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            CreateWindowCore();
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.Invoke(CreateWindowCore);
        }
    }

    private void CreateWindowCore()
    {
        if (_window is not null)
        {
            return;
        }

        _window = _serviceProvider.GetRequiredService<MainWindow>();
        _viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        _window.DataContext = _viewModel;

        System.Windows.Application.Current.MainWindow = _window;

        _window.PreviewKeyDown += OnWindowPreviewKeyDown;
        _window.Deactivated += OnWindowDeactivated;
        _window.Closing += OnWindowClosing;
    }

    private void ShowShell()
    {
        _window!.Dispatcher.Invoke(() =>
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
        if (_window is null)
        {
            return;
        }

        _window.Dispatcher.Invoke(() =>
        {
            if (_window.IsVisible)
            {
                _window.Hide();
            }
        });
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
        HideShell();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_viewModel is not null && !string.IsNullOrWhiteSpace(_viewModel.QueryText))
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
        if (_window is not null && _window.IsVisible)
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

        if (_window is not null)
        {
            _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
            _window.Deactivated -= OnWindowDeactivated;
            _window.Closing -= OnWindowClosing;
        }

        if (_toggleHotkeyId != 0)
        {
            _hotkeyService.UnregisterHotkey(_toggleHotkeyId);
            _toggleHotkeyId = 0;
        }

        _hotkeyService.Dispose();
    }
}
