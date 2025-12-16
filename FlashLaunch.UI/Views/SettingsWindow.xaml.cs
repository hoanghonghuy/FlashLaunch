using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlashLaunch.UI.ViewModels;
using FlashLaunch.UI.Localization;

namespace FlashLaunch.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        // Mặc định chọn mục General trong sidebar và chỉ hiển thị nội dung General
        NavGeneralButton.IsChecked = true;
        NavAppearanceButton.IsChecked = false;
        NavPluginsButton.IsChecked = false;
        NavAdvancedButton.IsChecked = false;

        GeneralContentPanel.Visibility = Visibility.Visible;
        AppearanceContentPanel.Visibility = Visibility.Collapsed;
        PluginsContentPanel.Visibility = Visibility.Collapsed;
        AdvancedContentPanel.Visibility = Visibility.Collapsed;
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnLanguageButtonClick(object sender, RoutedEventArgs e)
    {
        LanguagePopup.IsOpen = !LanguagePopup.IsOpen;
        ThemePopup.IsOpen = false;
    }

    private void OnLanguageOptionClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string tag } && !string.IsNullOrWhiteSpace(tag))
        {
            _viewModel.Language = tag;
            LanguagePopup.IsOpen = false;
        }
    }

    private void OnThemeButtonClick(object sender, RoutedEventArgs e)
    {
        ThemePopup.IsOpen = !ThemePopup.IsOpen;
        LanguagePopup.IsOpen = false;
    }

    private void OnThemeOptionClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string tag } && !string.IsNullOrWhiteSpace(tag))
        {
            _viewModel.Theme = tag;
            ThemePopup.IsOpen = false;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ApplySettings())
        {
            Close();
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.ApplySettings();
    }

    private void OnAddDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryAddCustomDirectory(_viewModel.NewDirectoryPath, out var error))
        {
            _viewModel.NewDirectoryPath = string.Empty;
            _viewModel.ValidationMessage = LocalizationManager.GetString("Settings_CustomDir_Added");
            _viewModel.IsValidationError = false;
        }
        else
        {
            _viewModel.ValidationMessage = error;
            _viewModel.IsValidationError = true;
        }
    }

    private void OnBrowseDirectoryClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        dialog.Description = LocalizationManager.GetString("Settings_BrowseDialog_Description");
        dialog.ShowNewFolderButton = false;

        var result = dialog.ShowDialog();
        if (result == System.Windows.Forms.DialogResult.OK &&
            !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _viewModel.NewDirectoryPath = dialog.SelectedPath;
            _viewModel.ValidationMessage = null;
            _viewModel.IsValidationError = false;
        }
    }

    private void OnRemoveDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string path } && !string.IsNullOrWhiteSpace(path))
        {
            _viewModel.RemoveCustomDirectory(path);
            _viewModel.ValidationMessage = string.Format(
                LocalizationManager.GetString("Settings_CustomDir_Removed"),
                path);
            _viewModel.IsValidationError = false;
        }
    }

    private void OnRefreshIndexClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshIndex();
    }

    private void OnReloadPluginsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ReloadPlugins();
    }

    private async void OnHealthCheckPluginsClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.HealthCheckPluginsAsync();
    }

    private void OnResetUsageLogsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetUsageLogs();
    }

    private void OnClearIconCacheClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearIconCache();
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnNavGeneralClick(object sender, RoutedEventArgs e)
    {
        NavGeneralButton.IsChecked = true;
        NavAppearanceButton.IsChecked = false;
        NavPluginsButton.IsChecked = false;
        NavAdvancedButton.IsChecked = false;

        GeneralContentPanel.Visibility = Visibility.Visible;
        AppearanceContentPanel.Visibility = Visibility.Collapsed;
        PluginsContentPanel.Visibility = Visibility.Collapsed;
        AdvancedContentPanel.Visibility = Visibility.Collapsed;

        ScrollToElement("GeneralSectionHeader");
    }

    private void OnNavAppearanceClick(object sender, RoutedEventArgs e)
    {
        NavGeneralButton.IsChecked = false;
        NavAppearanceButton.IsChecked = true;
        NavPluginsButton.IsChecked = false;
        NavAdvancedButton.IsChecked = false;

        GeneralContentPanel.Visibility = Visibility.Collapsed;
        AppearanceContentPanel.Visibility = Visibility.Visible;
        PluginsContentPanel.Visibility = Visibility.Collapsed;
        AdvancedContentPanel.Visibility = Visibility.Collapsed;

        ScrollToElement("AppearanceSectionHeader");
    }

    private void OnNavPluginsClick(object sender, RoutedEventArgs e)
    {
        NavGeneralButton.IsChecked = false;
        NavAppearanceButton.IsChecked = false;
        NavPluginsButton.IsChecked = true;
        NavAdvancedButton.IsChecked = false;

        GeneralContentPanel.Visibility = Visibility.Collapsed;
        AppearanceContentPanel.Visibility = Visibility.Collapsed;
        PluginsContentPanel.Visibility = Visibility.Visible;
        AdvancedContentPanel.Visibility = Visibility.Collapsed;

        ScrollToElement("PluginsSectionHeader");
    }

    private void OnNavAdvancedClick(object sender, RoutedEventArgs e)
    {
        NavGeneralButton.IsChecked = false;
        NavAppearanceButton.IsChecked = false;
        NavPluginsButton.IsChecked = false;
        NavAdvancedButton.IsChecked = true;

        GeneralContentPanel.Visibility = Visibility.Collapsed;
        AppearanceContentPanel.Visibility = Visibility.Collapsed;
        PluginsContentPanel.Visibility = Visibility.Collapsed;
        AdvancedContentPanel.Visibility = Visibility.Visible;

        ScrollToElement("AdvancedSectionHeader");
    }

    private void ScrollToElement(string elementName)
    {
        if (string.IsNullOrWhiteSpace(elementName))
        {
            return;
        }

        var element = FindName(elementName) as FrameworkElement;
        if (element is null)
        {
            return;
        }

        // Đảm bảo layout đã đo đạc trước khi scroll
        XamlContentRoot.UpdateLayout();

        var transform = element.TransformToAncestor(XamlContentRoot);
        var position = transform.Transform(new System.Windows.Point(0, 0));
        XamlContentRoot.ScrollToVerticalOffset(position.Y);
    }
}
