using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using FlashLaunch.UI.Views;
using FlashLaunch.UI.Localization;
using FlashLaunch.UI.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace FlashLaunch.UI.Services;

public sealed class TrayIconService(IServiceProvider serviceProvider, ShellController shellController) : IDisposable
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ShellController _shellController = shellController;
    private NotifyIcon? _notifyIcon;

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "FlashLaunch"
        };

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false
        };
        menu.Items.Add(LocalizationManager.GetString("Tray_Show"), null, (_, _) => ShowMainWindow());
        menu.Items.Add(LocalizationManager.GetString("Tray_Settings"), null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(LocalizationManager.GetString("Tray_Exit"), null, (_, _) => System.Windows.Application.Current.Shutdown());

        ApplyTheme(menu);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void ShowMainWindow()
    {
        _shellController.ShowMainWindow();
    }

    private void ShowSettings()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SettingsWindow? existingWindow = null;
            foreach (Window openWindow in System.Windows.Application.Current.Windows)
            {
                if (openWindow is SettingsWindow settingsWindow)
                {
                    existingWindow = settingsWindow;
                    break;
                }
            }

            if (existingWindow is not null)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

                existingWindow.ShowInTaskbar = true;
                existingWindow.Topmost = false;
                existingWindow.Activate();
                existingWindow.Focus();
                return;
            }

            var window = _serviceProvider.GetRequiredService<SettingsWindow>();
            window.ShowInTaskbar = true;
            window.Topmost = false;
            window.Show();
            window.Activate();
            window.Focus();
        });
    }

    public void Dispose()
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;

        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_notifyIcon?.ContextMenuStrip is ContextMenuStrip menu)
        {
            ApplyTheme(menu);
        }
    }

    private static void ApplyTheme(ContextMenuStrip menu)
    {
        var isDark = string.Equals(ThemeManager.ActualTheme, "Dark", StringComparison.OrdinalIgnoreCase);

        var background = isDark
            ? Color.FromArgb(32, 32, 32)
            : Color.White;
        var foreground = isDark
            ? Color.White
            : Color.Black;

        menu.BackColor = background;
        menu.ForeColor = foreground;
        menu.RenderMode = ToolStripRenderMode.System;

        foreach (ToolStripItem item in menu.Items)
        {
            if (item is null)
            {
                continue;
            }

            item.BackColor = background;
            item.ForeColor = foreground;
        }

        menu.ItemAdded -= OnMenuItemAdded;
        menu.ItemAdded += OnMenuItemAdded;
    }

    private static void OnMenuItemAdded(object? sender, ToolStripItemEventArgs e)
    {
        if (sender is not ContextMenuStrip menu || e.Item is null)
        {
            return;
        }

        e.Item.BackColor = menu.BackColor;
        e.Item.ForeColor = menu.ForeColor;
    }
}
