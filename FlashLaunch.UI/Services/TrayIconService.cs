using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using FlashLaunch.UI.Views;
using FlashLaunch.UI.Localization;
using FlashLaunch.UI.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace FlashLaunch.UI.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ShellController _shellController;
    private NotifyIcon? _notifyIcon;

    public TrayIconService(IServiceProvider serviceProvider, ShellController shellController)
    {
        _serviceProvider = serviceProvider;
        _shellController = shellController;
    }

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
            var window = _serviceProvider.GetRequiredService<SettingsWindow>();
            var owner = System.Windows.Application.Current.MainWindow;
            if (owner is not null && !ReferenceEquals(owner, window))
            {
                window.Owner = owner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            window.Show();
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

        menu.ItemAdded += (_, e) =>
        {
            if (e.Item is null)
            {
                return;
            }

            e.Item.BackColor = background;
            e.Item.ForeColor = foreground;
        };
    }
}
