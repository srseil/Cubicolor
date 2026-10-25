using System;
using System.Runtime.InteropServices;
using System.Text;
using Cubicolor.Simulation.Input;

namespace Cubicolor.Platform.Mac;

// macOS's virtual key codes (NSEvent.keyCode), which name physical keys as Apple's first keyboards laid them
// out, translated to Key; and the label the active keyboard layout puts on a key, for the controls menu.
internal static unsafe partial class KeyCodes
{
    // Virtual key code → Key, for the 128 codes. Unknown where a code names no key (or fn, which the
    // hardware handles itself).
    private static readonly Key[] Table =
    {
        /*   0 */ Key.A, Key.S, Key.D, Key.F, Key.H, Key.G, Key.Z, Key.X,
        /*   8 */ Key.C, Key.V, Key.NonUsBackSlash, Key.B, Key.Q, Key.W, Key.E, Key.R,
        /*  16 */ Key.Y, Key.T, Key.Alpha1, Key.Alpha2, Key.Alpha3, Key.Alpha4, Key.Alpha6, Key.Alpha5,
        /*  24 */ Key.Equals, Key.Alpha9, Key.Alpha7, Key.Minus, Key.Alpha8, Key.Alpha0, Key.Rightbracket, Key.O,
        /*  32 */ Key.U, Key.Leftbracket, Key.I, Key.P, Key.Return, Key.L, Key.J, Key.Apostrophe,
        /*  40 */ Key.K, Key.Semicolon, Key.Backslash, Key.Comma, Key.Slash, Key.N, Key.M, Key.Period,
        /*  48 */ Key.Tab, Key.Space, Key.Grave, Key.Backspace, Key.KpEnter, Key.Escape, Key.RGUI, Key.LGUI,
        /*  56 */ Key.LShift, Key.Capslock, Key.LAlt, Key.LCtrl, Key.RShift, Key.RAlt, Key.RCtrl, Key.Unknown,
        /*  64 */ Key.F17, Key.KpPeriod, Key.Unknown, Key.KpMultiply, Key.Unknown, Key.KpPlus, Key.Unknown, Key.NumLockClear,
        /*  72 */ Key.VolumeUp, Key.VolumeDown, Key.Mute, Key.KpDivide, Key.KpEnter, Key.Unknown, Key.KpMinus, Key.F18,
        /*  80 */ Key.F19, Key.KpEquals, Key.Kp0, Key.Kp1, Key.Kp2, Key.Kp3, Key.Kp4, Key.Kp5,
        /*  88 */ Key.Kp6, Key.Kp7, Key.Unknown, Key.Kp8, Key.Kp9, Key.International3, Key.International1, Key.KpComma,
        /*  96 */ Key.F5, Key.F6, Key.F7, Key.F3, Key.F8, Key.F9, Key.Lang2, Key.F11,
        /* 104 */ Key.Lang1, Key.F13, Key.F16, Key.F14, Key.Unknown, Key.F10, Key.Application, Key.F12,
        /* 112 */ Key.Unknown, Key.F15, Key.Help, Key.Home, Key.Pageup, Key.Delete, Key.F4, Key.End,
        /* 120 */ Key.F2, Key.Pagedown, Key.F1, Key.Left, Key.Right, Key.Down, Key.Up, Key.Power,
    };

    // The physical key behind a virtual key code. On an ISO keyboard (the German layout's), macOS reports
    // the key left of 1 (^ on German) as code 10 and the key left of Z (<) as 50, the other way round from
    // their positions, so the two are swapped back here, as SDL did; otherwise the console key, which is
    // the key left of 1, would be the wrong key.
    public static Key ToKey(ushort code)
    {
        if ((code == 10 || code == 50) && IsIso) code = (ushort) (60 - code);
        return code < Table.Length ? Table[code] : Key.Unknown;
    }

    // The virtual key code of a Key: the first code that maps to it, ISO swap included; -1 for none.
    public static int ToCode(Key key)
    {
        for (int code = 0; code < Table.Length; code++)
            if (ToKey((ushort) code) == key) return code;
        return -1;
    }

    private static bool IsIso => KBGetLayoutType(LMGetKbdType()) == 0x49534F20;   // 'ISO '

    // The key's label for the controls menu: the character the active layout puts on a printing key
    // (upper case, so "Z" on a German board where the US one has Y), else its name ("Tab", "Left Shift").
    public static string Name(Key key)
    {
        string? fixedName = key switch
        {
            Key.Return => "Return", Key.Escape => "Escape", Key.Backspace => "Delete", Key.Tab => "Tab",
            Key.Space => "Space", Key.Delete => "Forward Delete", Key.Capslock => "Caps Lock",
            Key.LCtrl => "Left Control", Key.RCtrl => "Right Control", Key.LShift => "Left Shift",
            Key.RShift => "Right Shift", Key.LAlt => "Left Option", Key.RAlt => "Right Option",
            Key.LGUI => "Left Command", Key.RGUI => "Right Command", Key.Up => "Up", Key.Down => "Down",
            Key.Left => "Left", Key.Right => "Right", Key.Home => "Home", Key.End => "End",
            Key.Pageup => "Page Up", Key.Pagedown => "Page Down", Key.KpEnter => "Enter",
            _ => null,
        };
        if (fixedName is not null) return fixedName;
        if (key is >= Key.F1 and <= Key.F12) return $"F{key - Key.F1 + 1}";
        if (key is >= Key.F13 and <= Key.F24) return $"F{key - Key.F13 + 13}";
        string? printed = Character(key);
        return string.IsNullOrWhiteSpace(printed) ? key.ToString() : printed.ToUpperInvariant();
    }

    // What the active layout types for the key with no modifiers held, or null. Dead keys report their
    // own character (^ on German), not a pending accent.
    public static string? Character(Key key)
    {
        int code = ToCode(key);
        if (code < 0) return null;

        IntPtr source = TISCopyCurrentKeyboardLayoutInputSource();
        if (source == IntPtr.Zero) return null;
        try
        {
            IntPtr data = TISGetInputSourceProperty(source, CoreFoundation.Constant(Carbon, "kTISPropertyUnicodeKeyLayoutData"));
            if (data == IntPtr.Zero) return null;
            uint deadKeyState = 0;
            char* chars = stackalloc char[8];
            int status = UCKeyTranslate(CoreFoundation.CFDataGetBytePtr(data), (ushort) code, 3 /* kUCKeyActionDisplay */, 0,
                LMGetKbdType(), 1 /* kUCKeyTranslateNoDeadKeysMask */, &deadKeyState, 8, out nint length, chars);
            return status == 0 && length > 0 ? new string(chars, 0, (int) length) : null;
        }
        finally
        {
            CoreFoundation.CFRelease(source);
        }
    }

    private const string CarbonPath = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private static readonly IntPtr Carbon = NativeLibrary.Load(CarbonPath);

    [LibraryImport(CarbonPath)] private static partial byte LMGetKbdType();
    [LibraryImport(CarbonPath)] private static partial uint KBGetLayoutType(short keyboardType);
    [LibraryImport(CarbonPath)] private static partial IntPtr TISCopyCurrentKeyboardLayoutInputSource();
    [LibraryImport(CarbonPath)] private static partial IntPtr TISGetInputSourceProperty(IntPtr source, IntPtr key);
    [LibraryImport(CarbonPath)] private static partial int UCKeyTranslate(byte* layout, ushort code, ushort action, uint modifiers,
        uint keyboardType, uint options, uint* deadKeyState, nint maxLength, out nint length, char* chars);

    // The SF Symbol for a key that has one (arrows, return, delete, escape, tab, space and the modifiers), for
    // the controls screen; null for letters, digits and the rest, which are shown as the layout prints them.
    public static string? Symbol(Key key) => key switch
    {
        Key.Up => "arrow.up",
        Key.Down => "arrow.down",
        Key.Left => "arrow.left",
        Key.Right => "arrow.right",
        Key.Return or Key.KpEnter => "return",
        Key.Backspace => "delete.left",
        Key.Delete => "delete.right",
        Key.Escape => "escape",
        Key.Tab => "arrow.right.to.line",
        Key.Space => "space",
        Key.LShift or Key.RShift => "shift",
        Key.LCtrl or Key.RCtrl => "control",
        Key.LAlt or Key.RAlt => "option",
        Key.LGUI or Key.RGUI => "command",
        Key.Capslock => "capslock",
        _ => null,
    };
}
