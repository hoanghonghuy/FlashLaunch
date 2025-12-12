using System;
using System.Windows;

namespace FlashLaunch.UI.Services;

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        app.Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(text));
    }
}
