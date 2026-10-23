using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.Simulation.Saves;

// Readers for what the earlier versions left on players' disks (docs/port-plan.md §2). Each takes the
// file's contents and returns what it could make of them, or null when nothing in it is usable: these are
// other programs' files, so anything malformed is skipped, never thrown. They never write.
public static class LegacySaves
{
    // --- Godot (v1.2, Windows) ---

    // user://progress.bin: three bytes, the solved count for normal, smart and genius; 255 is -1, locked.
    public static Progress? ReadGodotProgress(ReadOnlySpan<byte> data)
    {
        if (data.Length < 3) return null;
        var progress = new Progress();
        for (int i = 0; i < 3; i++)
        {
            int solved = data[i] == 255 ? Progress.Locked : data[i];
            if (solved > Level.PerDifficulty) return null;
            progress.Raise((Difficulty) i, solved);
        }
        return progress;
    }

    // project.godot as ProjectSettings.save() wrote it into the working directory, which under Steam is the
    // install directory. Godot never read it back, so it holds the choices the player last made and lost.
    // Its progress keys are ignored: the file may have shipped in the depot with the developer's own values.
    public static Settings? ReadGodotSettings(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string section = "";
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == ';') continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1];
                continue;
            }
            int equals = line.IndexOf('=');
            if (equals > 0) values[section + "/" + line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        if (!values.ContainsKey("application/config/name")) return null;

        var s = new Settings();
        if (Bool(values, "application/config/dark_background") is { } dark) s.DarkBackground = dark;
        if (Bool(values, "application/config/hide_ui") is { } hide) s.HideUi = hide;
        if (Bool(values, "audio/music_muted") is { } musicMuted) s.MusicMuted = musicMuted;
        if (Bool(values, "audio/sound_muted") is { } soundMuted) s.SoundMuted = soundMuted;
        if (Number(values, "audio/music_volume") is { } music) s.MusicVolume = Settings.ClampVolume(music);
        if (Number(values, "audio/sound_volume") is { } sound) s.SoundVolume = Settings.ClampVolume(sound);
        if (Bool(values, "display/window/size/fullscreen") is { } fullscreen) s.Fullscreen = fullscreen;
        if (Bool(values, "display/window/vsync/use_vsync") is { } vsync) s.VSync = vsync;
        if (Number(values, "display/window/size/width") is { } width && Number(values, "display/window/size/height") is { } height)
            s.WindowSize = WindowSize((int) width, (int) height);
        foreach (var (action, name) in KeyNames)
            if (Number(values, "application/config/key_" + name) is { } code && GodotKey((int) code) is { } key)
                s.Keys[action] = key;
        return s;
    }

    // --- libGDX (v1.1, macOS and Linux) ---

    // savestates/savestate.xml: <savestate><normal><level1>true</level1>... per difficulty.
    public static Progress? ReadJavaSaveState(string xml)
    {
        XElement? root = Xml(xml);
        if (root?.Name != "savestate") return null;
        var progress = new Progress();
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            XElement? levels = root.Element(difficulty.ToString().ToLowerInvariant());
            if (levels == null) continue;
            // Levels open in order, so the solved run from level 1 is the whole record.
            int solved = 0;
            while (solved < Level.PerDifficulty && IsTrue(levels.Element($"level{solved + 1}"))) solved++;
            if (solved > 0) progress.Raise(difficulty, solved);
        }
        return progress;
    }

    // config/config.xml: <config><settings>...</settings><controls>...</controls></config>.
    public static Settings? ReadJavaConfig(string xml)
    {
        XElement? root = Xml(xml);
        if (root?.Name != "config") return null;
        var s = new Settings();
        if (root.Element("settings") is { } settings)
        {
            if (Bool(settings, "dark-background") is { } dark) s.DarkBackground = dark;
            if (Bool(settings, "ui-hidden") is { } hide) s.HideUi = hide;
            if (Bool(settings, "music-muted") is { } musicMuted) s.MusicMuted = musicMuted;
            if (Bool(settings, "sound-muted") is { } soundMuted) s.SoundMuted = soundMuted;
            if (Int(settings, "music-volume") is { } music) s.MusicVolume = Settings.ClampVolume(music / 100.0);
            if (Int(settings, "sound-volume") is { } sound) s.SoundVolume = Settings.ClampVolume(sound / 100.0);
            if (Bool(settings, "fullscreen") is { } fullscreen) s.Fullscreen = fullscreen;
            if (Bool(settings, "vsync") is { } vsync) s.VSync = vsync;
            if (settings.Element("resolution")?.Value is { } resolution && SaveFile.TrySize(resolution.Trim(), out var size))
                s.WindowSize = WindowSize(size.Width, size.Height);
        }
        if (root.Element("controls") is { } controls)
            foreach (var (action, name) in KeyNames)
                if (Int(controls, name) is { } code && LibGdxKey(code) is { } key)
                    s.Keys[action] = key;
        return s;
    }

    private static readonly (GameAction, string)[] KeyNames =
    [
        (GameAction.Forwards, "forwards"),
        (GameAction.Backwards, "backwards"),
        (GameAction.Left, "left"),
        (GameAction.Right, "right"),
        (GameAction.Restart, "restart"),
        (GameAction.Pause, "pause"),
    ];

    // Both versions defaulted to 800x600, which says nothing about what the player wanted.
    private static (int, int)? WindowSize(int width, int height) =>
        (width, height) == (800, 600) || !SaveFile.TrySize($"{width}x{height}", out var size) ? null : size;

    // --- Key codes ---

    // Godot 3's key codes (core/os/keyboard.h in 3.0.2): printable keys are their (upper-case) character,
    // the rest SPKEY (1 << 24) plus an index.
    internal static Key? GodotKey(int code)
    {
        const int spkey = 1 << 24;
        if (code >= 'A' && code <= 'Z') return Key.A + (code - 'A');
        if (code >= '1' && code <= '9') return Key.Alpha1 + (code - '1');
        if (code >= spkey + 0x1C && code <= spkey + 0x27) return Key.F1 + (code - spkey - 0x1C);
        if (code >= spkey + 0x87 && code <= spkey + 0x8F) return Key.Kp1 + (code - spkey - 0x87);
        Key key = code switch
        {
            '0' => Key.Alpha0,
            ' ' => Key.Space,
            '\'' => Key.Apostrophe,
            ',' => Key.Comma,
            '-' => Key.Minus,
            '.' => Key.Period,
            '/' => Key.Slash,
            ';' => Key.Semicolon,
            '=' => Key.Equals,
            '[' => Key.Leftbracket,
            '\\' => Key.Backslash,
            ']' => Key.Rightbracket,
            '`' => Key.Grave,
            spkey + 0x01 => Key.Escape,
            spkey + 0x02 => Key.Tab,
            spkey + 0x04 => Key.Backspace,
            spkey + 0x05 => Key.Return,
            spkey + 0x06 => Key.KpEnter,
            spkey + 0x07 => Key.Insert,
            spkey + 0x08 => Key.Delete,
            spkey + 0x0D => Key.Home,
            spkey + 0x0E => Key.End,
            spkey + 0x0F => Key.Left,
            spkey + 0x10 => Key.Up,
            spkey + 0x11 => Key.Right,
            spkey + 0x12 => Key.Down,
            spkey + 0x13 => Key.Pageup,
            spkey + 0x14 => Key.Pagedown,
            spkey + 0x15 => Key.LShift,
            spkey + 0x16 => Key.LCtrl,
            spkey + 0x17 => Key.LGUI,
            spkey + 0x18 => Key.LAlt,
            spkey + 0x81 => Key.KpMultiply,
            spkey + 0x82 => Key.KpDivide,
            spkey + 0x83 => Key.KpMinus,
            spkey + 0x84 => Key.KpPeriod,
            spkey + 0x85 => Key.KpPlus,
            spkey + 0x86 => Key.Kp0,
            _ => Key.Unknown,
        };
        return key == Key.Unknown ? null : key;
    }

    // libGDX 1.7.1's Input.Keys (read from the shipped gdx jar's class file).
    internal static Key? LibGdxKey(int code)
    {
        if (code >= 29 && code <= 54) return Key.A + (code - 29);
        if (code >= 8 && code <= 16) return Key.Alpha1 + (code - 8);
        if (code >= 145 && code <= 153) return Key.Kp1 + (code - 145);
        if (code >= 244 && code <= 255) return Key.F1 + (code - 244);
        Key key = code switch
        {
            7 => Key.Alpha0,
            144 => Key.Kp0,
            19 => Key.Up,
            20 => Key.Down,
            21 => Key.Left,
            22 => Key.Right,
            3 => Key.Home,
            132 => Key.End,
            92 => Key.Pageup,
            93 => Key.Pagedown,
            133 => Key.Insert,
            112 => Key.Delete,
            67 => Key.Backspace,
            66 => Key.Return,
            131 => Key.Escape,
            61 => Key.Tab,
            62 => Key.Space,
            55 => Key.Comma,
            56 => Key.Period,
            69 => Key.Minus,
            70 => Key.Equals,
            71 => Key.Leftbracket,
            72 => Key.Rightbracket,
            73 => Key.Backslash,
            74 => Key.Semicolon,
            75 => Key.Apostrophe,
            76 => Key.Slash,
            68 => Key.Grave,
            57 => Key.LAlt,
            58 => Key.RAlt,
            59 => Key.LShift,
            60 => Key.RShift,
            129 => Key.LCtrl,
            130 => Key.RCtrl,
            _ => Key.Unknown,
        };
        return key == Key.Unknown ? null : key;
    }

    // --- Helpers ---

    private static XElement? Xml(string text)
    {
        try
        {
            // libGDX wrote `<?xml version="1.1"`, which .NET's reader rejects; the documents are plain 1.0 XML.
            int declarationEnd = text.StartsWith("<?xml", StringComparison.Ordinal) ? text.IndexOf("?>", StringComparison.Ordinal) : -1;
            if (declarationEnd >= 0) text = text[(declarationEnd + 2)..];
            return XDocument.Parse(text).Root;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static bool IsTrue(XElement? element) => element != null && element.Value.Trim() == "true";

    private static bool? Bool(XElement parent, string name) =>
        parent.Element(name)?.Value.Trim() switch { "true" => true, "false" => false, _ => null };

    private static int? Int(XElement parent, string name) =>
        parent.Element(name)?.Value is { } text && SaveFile.TryInt(text.Trim(), out int value) ? value : null;

    private static bool? Bool(Dictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key) switch { "true" => true, "false" => false, _ => null };

    private static double? Number(Dictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key) is { } text && SaveFile.TryDouble(text, out double value) ? value : null;
}
