using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cubicolor.Simulation.Saves;

// The game's own save files: plain `key = value` lines, `#` comments, and a `version` line so a later
// format can tell an older one apart. Written whole to a temporary file and renamed over the old one, so a
// crash mid-write leaves the previous file intact.
public static class SaveFile
{
    public const int Version = 1;

    public static Dictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int equals = line.IndexOf('=');
            if (equals <= 0) continue;
            values[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        return values;
    }

    public static string Format(string title, IEnumerable<(string Key, string Value)> values)
    {
        var text = new StringBuilder();
        text.Append("# ").Append(title).Append('\n');
        text.Append("version = ").Append(Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var (key, value) in values) text.Append(key).Append(" = ").Append(value).Append('\n');
        return text.ToString();
    }

    public static void WriteAtomically(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    // --- Progress ---

    public static string FormatProgress(Progress progress) => Format("Cubicolor progress", ProgressValues(progress));

    private static IEnumerable<(string, string)> ProgressValues(Progress progress)
    {
        foreach (var difficulty in Enum.GetValues<Levels.Difficulty>())
            yield return (Name(difficulty), progress.Solved(difficulty).ToString(CultureInfo.InvariantCulture));
    }

    public static Progress ParseProgress(string text)
    {
        var values = Parse(text);
        var progress = new Progress();
        foreach (var difficulty in Enum.GetValues<Levels.Difficulty>())
            if (values.TryGetValue(Name(difficulty), out string? value) && TryInt(value, out int solved))
                progress.Raise(difficulty, solved);
        return progress;
    }

    // --- Settings ---

    public static string FormatSettings(Settings settings) => Format("Cubicolor settings", SettingsValues(settings));

    private static IEnumerable<(string, string)> SettingsValues(Settings s)
    {
        yield return ("dark_background", Bool(s.DarkBackground));
        yield return ("hide_ui", Bool(s.HideUi));
        yield return ("music_volume", s.MusicVolume.ToString("0.###", CultureInfo.InvariantCulture));
        yield return ("sound_volume", s.SoundVolume.ToString("0.###", CultureInfo.InvariantCulture));
        yield return ("music_muted", Bool(s.MusicMuted));
        yield return ("sound_muted", Bool(s.SoundMuted));
        yield return ("fullscreen", Bool(s.Fullscreen));
        yield return ("vsync", Bool(s.VSync));
        if (s.Language is { } language)
            yield return ("language", language.Code());
        if (s.WindowSize is var (width, height))
            yield return ("window_size", FormattableString.Invariant($"{width}x{height}"));
        foreach (var (action, key) in s.Keys)
            yield return ("key." + Name(action), key.ToString());
        foreach (var (action, button) in s.Pad)
            yield return ("pad." + Name(action), button.ToString());
    }

    public static Settings ParseSettings(string text)
    {
        var values = Parse(text);
        var s = new Settings();
        if (TryBool(values, "dark_background", out bool b)) s.DarkBackground = b;
        if (TryBool(values, "hide_ui", out b)) s.HideUi = b;
        if (TryBool(values, "music_muted", out b)) s.MusicMuted = b;
        if (TryBool(values, "sound_muted", out b)) s.SoundMuted = b;
        if (TryBool(values, "fullscreen", out b)) s.Fullscreen = b;
        if (TryBool(values, "vsync", out b)) s.VSync = b;
        if (values.TryGetValue("music_volume", out string? v) && TryDouble(v, out double d)) s.MusicVolume = Settings.ClampVolume(d);
        if (values.TryGetValue("sound_volume", out v) && TryDouble(v, out d)) s.SoundVolume = Settings.ClampVolume(d);
        if (values.TryGetValue("window_size", out v) && TrySize(v, out var size)) s.WindowSize = size;
        if (values.TryGetValue("language", out v)) s.Language = Languages.FromCode(v);
        foreach (GameAction action in Enum.GetValues<GameAction>())
            if (values.TryGetValue("key." + Name(action), out v) && Enum.TryParse(v, out Input.Key key) && Enum.IsDefined(key) && key != Input.Key.Unknown)
                s.Keys[action] = key;
        foreach (GameAction action in Enum.GetValues<GameAction>())
            if (values.TryGetValue("pad." + Name(action), out v) && Enum.TryParse(v, out Input.PadButton button) && Enum.IsDefined(button))
                s.Pad[action] = button;
        return s;
    }

    // --- Values ---

    private static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    private static string Bool(bool value) => value ? "true" : "false";

    private static bool TryBool(Dictionary<string, string> values, string key, out bool result)
    {
        result = false;
        if (!values.TryGetValue(key, out string? value)) return false;
        return bool.TryParse(value, out result);
    }

    internal static bool TryInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    internal static bool TryDouble(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    // "1280x720", with a sane range: anything else is no size at all.
    internal static bool TrySize(string text, out (int Width, int Height) size)
    {
        size = default;
        string[] parts = text.Split('x');
        if (parts.Length != 2 || !TryInt(parts[0], out int width) || !TryInt(parts[1], out int height)) return false;
        if (width < 320 || height < 240 || width > 16384 || height > 16384) return false;
        size = (width, height);
        return true;
    }
}
