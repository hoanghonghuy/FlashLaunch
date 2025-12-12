using System.Windows;

namespace FlashLaunch.UI.Views;

public partial class ConfirmDialog : Window
{
    private ConfirmDialog()
    {
        InitializeComponent();
    }

    public static bool Show(Window? owner, string title, string message)
    {
        var dialog = new ConfirmDialog
        {
            Owner = owner,
            Title = title
        };

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;

        var result = dialog.ShowDialog();
        return result == true;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
