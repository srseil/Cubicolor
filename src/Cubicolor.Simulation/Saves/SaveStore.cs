using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cubicolor.Simulation.Saves;

// Where saves live: the game's own data directory, and every place an earlier version may have left one.
public sealed record SaveLocations(string DataDirectory, IReadOnlyList<string> GodotUserDirectories, IReadOnlyList<string> InstallDirectories)
{
    public enum Platform { MacOS, Windows, Linux }

    // The places for a platform, from its environment (`env` reads a variable, null when unset) and the
    // directories the game runs from. Nothing here touches the disk.
    public static SaveLocations For(Platform platform, Func<string, string?> env, IEnumerable<string> installDirectories)
    {
        string home = env("HOME") ?? env("USERPROFILE") ?? ".";
        string data = platform switch
        {
            Platform.MacOS => Path.Combine(home, "Library", "Application Support", "Cubicolor"),
            Platform.Windows => Path.Combine(env("APPDATA") ?? Path.Combine(home, "AppData", "Roaming"), "Cubicolor"),
            _ => Path.Combine(NonEmpty(env("XDG_DATA_HOME")) ?? Path.Combine(home, ".local", "share"), "Cubicolor"),
        };

        // Godot 3.0's user:// for a project without a custom user dir (OS::get_user_data_dir): the data path,
        // then "Godot" on Windows ("godot" elsewhere), "app_userdata" and the project name. Windows checks
        // XDG_DATA_HOME before APPDATA too.
        string? godotData = platform switch
        {
            Platform.Windows => NonEmpty(env("XDG_DATA_HOME")) ?? env("APPDATA"),
            Platform.Linux => NonEmpty(env("XDG_DATA_HOME")) ?? Path.Combine(home, ".local", "share"),
            _ => null,
        };
        var godot = new List<string>();
        if (godotData != null)
            godot.Add(Path.Combine(godotData, platform == Platform.Windows ? "Godot" : "godot", "app_userdata", "Cubicolor"));

        return new SaveLocations(data, godot, installDirectories.Distinct().ToList());
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

// The player's progress and settings, loaded once at start and written back on every change.
//
// Progress is merged on every start: the game's own file and every legacy record found, the maximum per
// difficulty (port-plan §2.2). Settings come from the game's own file; when there is none yet, from the
// most recently written legacy one, once. Legacy files are only ever read.
public sealed class SaveStore
{
    public const string ProgressFile = "progress.txt";
    public const string SettingsFile = "settings.txt";

    private readonly SaveLocations _locations;

    public Progress Progress { get; }
    public Settings Settings { get; }
    // Where each value came from, for the log.
    public IReadOnlyList<string> Sources { get; }

    private SaveStore(SaveLocations locations, Progress progress, Settings settings, List<string> sources)
    {
        _locations = locations;
        Progress = progress;
        Settings = settings;
        Sources = sources;
    }

    public static SaveStore Load(SaveLocations locations)
    {
        var sources = new List<string>();
        var progress = new Progress();
        string own = Path.Combine(locations.DataDirectory, ProgressFile);
        if (ReadText(own) is { } ownProgress)
        {
            progress.Merge(SaveFile.ParseProgress(ownProgress));
            sources.Add($"progress: {own} ({progress})");
        }
        foreach (var (path, legacy) in LegacyProgress(locations))
        {
            progress.Merge(legacy);
            sources.Add($"progress: {path} ({legacy})");
        }

        Settings settings;
        string ownSettingsPath = Path.Combine(locations.DataDirectory, SettingsFile);
        if (ReadText(ownSettingsPath) is { } ownSettings)
        {
            settings = SaveFile.ParseSettings(ownSettings);
            sources.Add($"settings: {ownSettingsPath}");
        }
        else if (LegacySettings(locations).Where(s => s.Settings != null).OrderByDescending(s => s.Written).FirstOrDefault() is { Settings: not null } newest)
        {
            settings = newest.Settings;
            sources.Add($"settings: {newest.Path} (imported)");
        }
        else
        {
            settings = new Settings();
        }

        var store = new SaveStore(locations, progress, settings, sources);
        // Write both at once, so the next start finds the game's own files and the import never repeats.
        store.SaveProgress();
        store.SaveSettings();
        return store;
    }

    public void SaveProgress() =>
        SaveFile.WriteAtomically(Path.Combine(_locations.DataDirectory, ProgressFile), SaveFile.FormatProgress(Progress));

    public void SaveSettings() =>
        SaveFile.WriteAtomically(Path.Combine(_locations.DataDirectory, SettingsFile), SaveFile.FormatSettings(Settings));

    private static IEnumerable<(string Path, Progress Progress)> LegacyProgress(SaveLocations locations)
    {
        foreach (string directory in locations.GodotUserDirectories)
        {
            string path = Path.Combine(directory, "progress.bin");
            if (ReadBytes(path) is { } data && LegacySaves.ReadGodotProgress(data) is { } progress)
                yield return (path, progress);
        }
        foreach (string directory in locations.InstallDirectories)
        {
            string path = Path.Combine(directory, "savestates", "savestate.xml");
            if (ReadText(path) is { } xml && LegacySaves.ReadJavaSaveState(xml) is { } progress)
                yield return (path, progress);
        }
    }

    private static IEnumerable<(string Path, DateTime Written, Settings? Settings)> LegacySettings(SaveLocations locations)
    {
        foreach (string directory in locations.InstallDirectories)
        {
            string godot = Path.Combine(directory, "project.godot");
            if (ReadText(godot) is { } text) yield return (godot, File.GetLastWriteTimeUtc(godot), LegacySaves.ReadGodotSettings(text));
            string java = Path.Combine(directory, "config", "config.xml");
            if (ReadText(java) is { } xml) yield return (java, File.GetLastWriteTimeUtc(java), LegacySaves.ReadJavaConfig(xml));
        }
    }

    // Other programs' files and our own, read at the I/O boundary: unreadable counts as absent.
    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static byte[]? ReadBytes(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
