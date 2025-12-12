using System.Windows;
using FlashLaunch.UI.Views;

namespace FlashLaunch.UI.Services;

public sealed class ConfirmDialogService : IConfirmDialogService
{
    public bool Show(string title, string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow as System.Windows.Window;
        return ConfirmDialog.Show(owner, title, message);
    }
}
