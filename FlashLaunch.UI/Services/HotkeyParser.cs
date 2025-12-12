using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using FlashLaunch.UI.Interop;

namespace FlashLaunch.UI.Services;

public static class HotkeyParser
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "alt", HotkeyModifiers.Alt },
        { "ctrl", HotkeyModifiers.Control },
        { "control", HotkeyModifiers.Control },
        { "shift", HotkeyModifiers.Shift },
        { "win", HotkeyModifiers.Win },
        { "windows", HotkeyModifiers.Win }
    };

    public static bool TryParse(string? text, out HotkeyModifiers modifiers, out Key key)
    {
        modifiers = HotkeyModifiers.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToArray();

        if (parts.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (ModifierMap.TryGetValue(parts[i], out var modifier))
            {
                modifiers |= modifier;
            }
            else
            {
                return false;
            }
        }

        var keyToken = parts[^1];
        if (ModifierMap.ContainsKey(keyToken))
        {
            // key must not be a modifier
            return false;
        }

        if (Enum.TryParse(keyToken, true, out key))
        {
            return true;
        }

        return false;
    }

    public static string Normalize(string text)
    {
        if (!TryParse(text, out var modifiers, out var key))
        {
            return text;
        }

        var tokens = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) tokens.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) tokens.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) tokens.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) tokens.Add("Win");
        tokens.Add(key.ToString());
        return string.Join(" + ", tokens);
    }
}
