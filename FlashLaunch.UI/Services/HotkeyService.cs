using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FlashLaunch.UI.Interop;

namespace FlashLaunch.UI.Services;

public sealed class HotkeyService : IHotkeyService
{
    private readonly ConcurrentDictionary<int, Action> _callbacks = new();
    private HwndSource? _source;
    private int _currentId;

    public void Initialize(Window window)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        if (_source is not null)
        {
            return;
        }

        window.SourceInitialized += (_, _) => Attach(window);

        if (window.IsLoaded)
        {
            Attach(window);
        }
    }

    private void Attach(Window window)
    {
        if (_source is not null)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle is not available.");
        }

        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("Cannot create HWND source.");
        _source.AddHook(WndProc);
    }

    public int RegisterHotkey(HotkeyModifiers modifiers, Key key, Action callback)
    {
        if (_source is null)
        {
            throw new InvalidOperationException("HotkeyService is not initialized.");
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        var id = Interlocked.Increment(ref _currentId);
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (!HotkeyInterop.RegisterHotKey(_source.Handle, id, (uint)modifiers, virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register hotkey.");
        }

        _callbacks[id] = callback;
        return id;
    }

    public void UnregisterHotkey(int id)
    {
        if (_source is null)
        {
            return;
        }

        if (_callbacks.TryRemove(id, out _))
        {
            HotkeyInterop.UnregisterHotKey(_source.Handle, id);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyInterop.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var callback))
            {
                callback.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null)
        {
            return;
        }

        foreach (var id in _callbacks.Keys)
        {
            HotkeyInterop.UnregisterHotKey(_source.Handle, id);
        }

        _callbacks.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }
}
