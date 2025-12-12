using System.Windows;
using System.Windows.Input;
using FlashLaunch.UI.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace FlashLaunch.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            ViewModel.MoveSelectionDown();
            if (ViewModel.SelectedResult is not null)
            {
                ResultsList.ScrollIntoView(ViewModel.SelectedResult);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            ViewModel.MoveSelectionUp();
            if (ViewModel.SelectedResult is not null)
            {
                ResultsList.ScrollIntoView(ViewModel.SelectedResult);
            }
            e.Handled = true;
            return;
        }
    }

    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            ViewModel.OpenSelectedLocation();

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await ViewModel.ExecuteSelectedAsync();
            e.Handled = true;
        }
    }

    private async void OnResultsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ExecuteSelectedAsync();
    }

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}