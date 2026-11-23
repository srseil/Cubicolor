using System;
using Cubicolor.Simulation.Input;

namespace Cubicolor.Platform.Windows;

// The keyboard's scan codes (set 1, as a key message's lParam carries them, with the 0xE0 prefix as the
// extended flag), which name physical keys whatever the layout, translated to Key; and the label the active
// layout puts on a key, for the controls menu.
internal static class ScanCodes
{
    // Scan code → Key, without the extended flag. Unknown where a code names no key.
    private static readonly Key[] Plain = Table(
        (0x01, Key.Escape), (0x02, Key.Alpha1), (0x03, Key.Alpha2), (0x04, Key.Alpha3), (0x05, Key.Alpha4), (0x06, Key.Alpha5),
        (0x07, Key.Alpha6), (0x08, Key.Alpha7), (0x09, Key.Alpha8), (0x0A, Key.Alpha9), (0x0B, Key.Alpha0), (0x0C, Key.Minus),
        (0x0D, Key.Equals), (0x0E, Key.Backspace), (0x0F, Key.Tab), (0x10, Key.Q), (0x11, Key.W), (0x12, Key.E), (0x13, Key.R),
        (0x14, Key.T), (0x15, Key.Y), (0x16, Key.U), (0x17, Key.I), (0x18, Key.O), (0x19, Key.P), (0x1A, Key.Leftbracket),
        (0x1B, Key.Rightbracket), (0x1C, Key.Return), (0x1D, Key.LCtrl), (0x1E, Key.A), (0x1F, Key.S), (0x20, Key.D), (0x21, Key.F),
        (0x22, Key.G), (0x23, Key.H), (0x24, Key.J), (0x25, Key.K), (0x26, Key.L), (0x27, Key.Semicolon), (0x28, Key.Apostrophe),
        (0x29, Key.Grave), (0x2A, Key.LShift), (0x2B, Key.Backslash), (0x2C, Key.Z), (0x2D, Key.X), (0x2E, Key.C), (0x2F, Key.V),
        (0x30, Key.B), (0x31, Key.N), (0x32, Key.M), (0x33, Key.Comma), (0x34, Key.Period), (0x35, Key.Slash), (0x36, Key.RShift),
        (0x37, Key.KpMultiply), (0x38, Key.LAlt), (0x39, Key.Space), (0x3A, Key.Capslock), (0x3B, Key.F1), (0x3C, Key.F2),
        (0x3D, Key.F3), (0x3E, Key.F4), (0x3F, Key.F5), (0x40, Key.F6), (0x41, Key.F7), (0x42, Key.F8), (0x43, Key.F9),
        (0x44, Key.F10), (0x45, Key.Pause), (0x46, Key.Scrolllock), (0x47, Key.Kp7), (0x48, Key.Kp8), (0x49, Key.Kp9),
        (0x4A, Key.KpMinus), (0x4B, Key.Kp4), (0x4C, Key.Kp5), (0x4D, Key.Kp6), (0x4E, Key.KpPlus), (0x4F, Key.Kp1), (0x50, Key.Kp2),
        (0x51, Key.Kp3), (0x52, Key.Kp0), (0x53, Key.KpPeriod), (0x56, Key.NonUsBackSlash), (0x57, Key.F11), (0x58, Key.F12),
        (0x59, Key.KpEquals), (0x64, Key.F13), (0x65, Key.F14), (0x66, Key.F15), (0x67, Key.F16), (0x68, Key.F17), (0x69, Key.F18),
        (0x6A, Key.F19), (0x6B, Key.F20), (0x6C, Key.F21), (0x6D, Key.F22), (0x6E, Key.F23), (0x73, Key.International1),
        (0x76, Key.F24), (0x7D, Key.International3), (0x7E, Key.KpComma), (0xF1, Key.Lang2), (0xF2, Key.Lang1));

    // With the extended flag (0xE0 before the code).
    private static readonly Key[] Extended = Table(
        (0x1C, Key.KpEnter), (0x1D, Key.RCtrl), (0x20, Key.Mute), (0x2E, Key.VolumeDown), (0x30, Key.VolumeUp),
        (0x35, Key.KpDivide), (0x37, Key.Printscreen), (0x38, Key.RAlt), (0x45, Key.NumLockClear), (0x47, Key.Home), (0x48, Key.Up),
        (0x49, Key.Pageup), (0x4B, Key.Left), (0x4D, Key.Right), (0x4F, Key.End), (0x50, Key.Down), (0x51, Key.Pagedown),
        (0x52, Key.Insert), (0x53, Key.Delete), (0x5B, Key.LGUI), (0x5C, Key.RGUI), (0x5D, Key.Application), (0x5E, Key.Power));

    private static Key[] Table(params (int Code, Key Key)[] entries)
    {
        var table = new Key[256];
        foreach (var (code, key) in entries) table[code] = key;
        return table;
    }

    // The physical key of a key message. Pause and Num Lock share a code with the other's flag the wrong way
    // round (Pause comes as 0x45 unextended, Num Lock as 0x45 extended, but the virtual key tells them apart).
    public static Key ToKey(int scanCode, bool extended, nuint virtualKey)
    {
        if (virtualKey == 0x13) return Key.Pause;          // VK_PAUSE
        if (virtualKey == 0x90) return Key.NumLockClear;   // VK_NUMLOCK
        return (extended ? Extended : Plain)[scanCode & 0xFF];
    }

    // The scan code of a Key and whether it is extended; null for none.
    public static (int Code, bool Extended)? ToCode(Key key)
    {
        int plain = Array.IndexOf(Plain, key), extended = Array.IndexOf(Extended, key);
        if (key == Key.Unknown) return null;
        if (plain > 0) return (plain, false);
        if (extended > 0) return (extended, true);
        return null;
    }

    // The key's label for the controls menu: the character the active layout puts on a printing key (upper
    // case, so "Z" on a German board where the US one has Y), else its name as a Windows keyboard prints it.
    public static string Name(Key key)
    {
        string? fixedName = key switch
        {
            Key.Return => "Enter", Key.KpEnter => "Num Enter", Key.Escape => "Esc", Key.Backspace => "Backspace", Key.Tab => "Tab",
            Key.Space => "Space", Key.Delete => "Delete", Key.Insert => "Insert", Key.Capslock => "Caps Lock",
            Key.LCtrl => "Left Ctrl", Key.RCtrl => "Right Ctrl", Key.LShift => "Left Shift", Key.RShift => "Right Shift",
            Key.LAlt => "Alt", Key.RAlt => "Alt Gr", Key.LGUI => "Left Windows", Key.RGUI => "Right Windows",
            Key.Application => "Menu", Key.Up => "Up", Key.Down => "Down", Key.Left => "Left", Key.Right => "Right",
            Key.Home => "Home", Key.End => "End", Key.Pageup => "Page Up", Key.Pagedown => "Page Down",
            Key.Printscreen => "Print Screen", Key.Scrolllock => "Scroll Lock", Key.Pause => "Pause", Key.NumLockClear => "Num Lock",
            _ => null,
        };
        if (fixedName is not null) return fixedName;
        if (key is >= Key.F1 and <= Key.F12) return $"F{key - Key.F1 + 1}";
        if (key is >= Key.F13 and <= Key.F24) return $"F{key - Key.F13 + 13}";
        if (key is >= Key.Kp1 and <= Key.Kp0) return "Num " + (key == Key.Kp0 ? 0 : key - Key.Kp1 + 1);
        string? printed = Character(key);
        return string.IsNullOrWhiteSpace(printed) ? key.ToString() : printed.ToUpperInvariant();
    }

    // What the active layout prints on the key, or null. Dead keys report their own character (^ on German).
    public static string? Character(Key key)
    {
        if (ToCode(key) is not var (code, extended)) return null;
        IntPtr layout = Win32.GetKeyboardLayout(0);
        uint virtualKey = Win32.MapVirtualKeyExW((uint) code | (extended ? 0xE000u : 0), 3 /* MAPVK_VSC_TO_VK_EX */, layout);
        if (virtualKey == 0) return null;
        // MAPVK_VK_TO_CHAR: the unshifted character in the low word, the top bit set for a dead key.
        uint character = Win32.MapVirtualKeyExW(virtualKey, 2, layout) & 0xFFFF;
        return character == 0 ? null : ((char) character).ToString();
    }
}
