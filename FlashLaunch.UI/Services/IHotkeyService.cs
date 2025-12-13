using System;
using System.Windows.Input;
using FlashLaunch.UI.Interop;

namespace FlashLaunch.UI.Services;

public interface IHotkeyService : IDisposable
{
    void Initialize();

    int RegisterHotkey(HotkeyModifiers modifiers, Key key, Action callback);

    void UnregisterHotkey(int id);
}
