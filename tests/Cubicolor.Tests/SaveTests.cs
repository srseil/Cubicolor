using System;
using System.Collections.Generic;
using System.IO;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;
using Xunit;

namespace Cubicolor.Tests;

public sealed class SaveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cubicolor-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string Legacy(string relative) => Repo.Fixture(Path.Combine("legacy", relative));

    private SaveLocations Locations(string? godotUser = null, params string[] install) =>
        new(Path.Combine(_root, "data"), godotUser == null ? [] : [godotUser], install);

    private string Dir(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    // --- Progress ---

    [Fact]
    public void A_fresh_player_has_only_the_first_normal_level_open()
    {
        var progress = new Progress();

        Assert.True(progress.IsOpen(Difficulty.Normal, 1));
        Assert.False(progress.IsOpen(Difficulty.Normal, 2));
        Assert.False(progress.IsOpen(Difficulty.Smart, 1));
    }

    [Fact]
    public void Finishing_a_difficulty_opens_the_next()
    {
        var progress = new Progress();
        for (int n = 1; n <= Level.PerDifficulty; n++) progress.Complete(Difficulty.Normal, n);

        Assert.Equal(0, progress.Solved(Difficulty.Smart));
        Assert.True(progress.IsOpen(Difficulty.Smart, 1));
        Assert.Equal(Progress.Locked, progress.Solved(Difficulty.Genius));
    }

    [Fact]
    public void Replaying_a_solved_level_changes_nothing_and_a_locked_one_cannot_be_completed()
    {
        var progress = new Progress();
        progress.Complete(Difficulty.Normal, 1);
        progress.Complete(Difficulty.Normal, 2);
        progress.Complete(Difficulty.Normal, 1);

        Assert.Equal(2, progress.Solved(Difficulty.Normal));
        Assert.Throws<InvalidOperationException>(() => progress.Complete(Difficulty.Normal, 5));
    }

    [Fact]
    public void Progress_and_settings_survive_their_own_files()
    {
        var progress = new Progress();
        progress.Merge(LegacySaves.ReadGodotProgress([16, 4, 255])!);
        var settings = new Settings { DarkBackground = true, MusicVolume = 0.25, WindowSize = (1600, 900) };
        settings.Keys[GameAction.Restart] = Key.R;
        settings.Pad[GameAction.Restart] = PadButton.West;

        Progress readProgress = SaveFile.ParseProgress(SaveFile.FormatProgress(progress));
        Settings read = SaveFile.ParseSettings(SaveFile.FormatSettings(settings));

        Assert.Equal(progress.ToString(), readProgress.ToString());
        Assert.True(read.DarkBackground);
        Assert.Equal(0.25, read.MusicVolume);
        Assert.Equal((1600, 900), read.WindowSize);
        Assert.Equal(Key.R, read.Keys[GameAction.Restart]);
        Assert.Equal(Key.Up, read.Keys[GameAction.Forwards]);
        Assert.Equal(PadButton.West, read.Pad[GameAction.Restart]);
        Assert.Equal(PadButton.Menu, read.Pad[GameAction.Pause]);
    }

    // --- Godot ---

    [Fact]
    public void Godot_progress_reads_its_three_bytes_with_255_as_locked()
    {
        Progress progress = LegacySaves.ReadGodotProgress([5, 255, 255])!;

        Assert.Equal(5, progress.Solved(Difficulty.Normal));
        Assert.Equal(Progress.Locked, progress.Solved(Difficulty.Smart));
        Assert.Equal(Progress.Locked, progress.Solved(Difficulty.Genius));
    }

    [Fact]
    public void A_real_godot_progress_file_reads()
    {
        Progress progress = LegacySaves.ReadGodotProgress(File.ReadAllBytes(Legacy("godot/progress.bin")))!;

        Assert.Equal("normal 16, smart 16, genius 16", progress.ToString());
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 3, 255 })]
    [InlineData(new byte[] { 17, 0, 0 })]
    public void A_truncated_or_impossible_godot_progress_file_is_ignored(byte[] data) =>
        Assert.Null(LegacySaves.ReadGodotProgress(data));

    [Fact]
    public void Godot_settings_read_from_the_project_file_godot_wrote_beside_the_game()
    {
        string text = File.ReadAllText(Legacy("godot/project.godot"))
            .Replace("config/dark_background=false", "config/dark_background=true")
            .Replace("music_volume=1.0", "music_volume=0.4")
            .Replace("config/key_restart=16777220", "config/key_restart=82")
            .Replace("window/size/width=800", "window/size/width=1920")
            .Replace("window/size/height=600", "window/size/height=1080");

        Settings settings = LegacySaves.ReadGodotSettings(text)!;

        Assert.True(settings.DarkBackground);
        Assert.Equal(0.4, settings.MusicVolume, 6);
        Assert.Equal(Key.R, settings.Keys[GameAction.Restart]);
        Assert.Equal(Key.Escape, settings.Keys[GameAction.Pause]);
        Assert.Equal(Key.Up, settings.Keys[GameAction.Forwards]);
        Assert.Equal((1920, 1080), settings.WindowSize);
    }

    [Fact]
    public void The_default_800x600_window_is_no_choice_at_all() =>
        Assert.Null(LegacySaves.ReadGodotSettings(File.ReadAllText(Legacy("godot/project.godot")))!.WindowSize);

    // --- libGDX ---

    [Fact]
    public void Java_savestates_count_the_solved_run_per_difficulty()
    {
        Assert.Equal("normal 16, smart 16, genius 16",
            LegacySaves.ReadJavaSaveState(File.ReadAllText(Legacy("java/savestate-all-solved.xml")))!.ToString());
        Assert.Equal("normal 12, smart -1, genius -1",
            LegacySaves.ReadJavaSaveState(File.ReadAllText(Legacy("java/savestate-partial.xml")))!.ToString());
    }

    [Fact]
    public void Java_config_reads_with_its_libgdx_key_codes_and_missing_fields()
    {
        // The real file predates the dark-background option: it simply isn't there.
        Settings settings = LegacySaves.ReadJavaConfig(File.ReadAllText(Legacy("java/config.xml")))!;

        Assert.False(settings.DarkBackground);
        Assert.True(settings.MusicMuted);
        Assert.Equal(0.43, settings.MusicVolume, 6);
        Assert.Equal(0.25, settings.SoundVolume, 6);
        Assert.Equal(Key.Backspace, settings.Keys[GameAction.Restart]);
        Assert.Equal(Key.Escape, settings.Keys[GameAction.Pause]);
        Assert.Equal(Key.Left, settings.Keys[GameAction.Left]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<config><settings>")]
    [InlineData("<savestate/>")]
    public void Malformed_java_files_are_ignored(string xml) => Assert.Null(LegacySaves.ReadJavaConfig(xml));

    [Fact]
    public void Key_codes_of_both_versions_map_to_physical_keys()
    {
        Assert.Equal(Key.W, LegacySaves.LibGdxKey(51));
        Assert.Equal(Key.Space, LegacySaves.LibGdxKey(62));
        Assert.Equal(Key.F12, LegacySaves.LibGdxKey(255));
        Assert.Null(LegacySaves.LibGdxKey(4));
        Assert.Equal(Key.W, LegacySaves.GodotKey('W'));
        Assert.Equal(Key.Down, LegacySaves.GodotKey(16777234));
        Assert.Equal(Key.Kp5, LegacySaves.GodotKey(16777355));
        Assert.Null(LegacySaves.GodotKey(16777217 + 0x2F));
    }

    // --- Locations ---

    [Fact]
    public void Godot_user_data_is_where_godot_3_put_it()
    {
        var env = new Dictionary<string, string?> { ["HOME"] = "/home/p", ["APPDATA"] = @"C:\Users\p\AppData\Roaming" };

        SaveLocations windows = SaveLocations.For(SaveLocations.Platform.Windows, k => env.GetValueOrDefault(k), []);
        SaveLocations linux = SaveLocations.For(SaveLocations.Platform.Linux, k => env.GetValueOrDefault(k), []);

        Assert.Equal(Path.Combine(@"C:\Users\p\AppData\Roaming", "Godot", "app_userdata", "Cubicolor"), windows.GodotUserDirectories[0]);
        Assert.Equal(Path.Combine("/home/p", ".local", "share", "godot", "app_userdata", "Cubicolor"), linux.GodotUserDirectories[0]);
        Assert.Equal(Path.Combine("/home/p", ".local", "share", "Cubicolor"), linux.DataDirectory);
    }

    // --- The store ---

    [Fact]
    public void A_first_start_merges_every_legacy_record_and_writes_the_games_own_files()
    {
        string godot = Dir("godot");
        File.WriteAllBytes(Path.Combine(godot, "progress.bin"), [16, 3, 255]);
        string install = Dir("install");
        Directory.CreateDirectory(Path.Combine(install, "savestates"));
        File.Copy(Legacy("java/savestate-partial.xml"), Path.Combine(install, "savestates", "savestate.xml"));

        SaveStore store = SaveStore.Load(Locations(godot, install));

        Assert.Equal("normal 16, smart 3, genius -1", store.Progress.ToString());
        Assert.True(File.Exists(Path.Combine(_root, "data", SaveStore.ProgressFile)));
        Assert.True(File.Exists(Path.Combine(_root, "data", SaveStore.SettingsFile)));
    }

    [Fact]
    public void Legacy_progress_still_counts_after_the_import_and_legacy_files_stay_untouched()
    {
        string godot = Dir("godot");
        string legacy = Path.Combine(godot, "progress.bin");
        File.WriteAllBytes(legacy, [2, 255, 255]);
        SaveStore first = SaveStore.Load(Locations(godot));
        first.Progress.Complete(Difficulty.Normal, 3);
        first.SaveProgress();

        // The player went back to the old build on a beta branch and got further there.
        File.WriteAllBytes(legacy, [7, 255, 255]);
        SaveStore second = SaveStore.Load(Locations(godot));

        Assert.Equal(7, second.Progress.Solved(Difficulty.Normal));
        Assert.Equal([7, 255, 255], File.ReadAllBytes(legacy));
    }

    [Fact]
    public void Settings_are_imported_once_from_the_newest_legacy_file()
    {
        string install = Dir("install");
        Directory.CreateDirectory(Path.Combine(install, "config"));
        string java = Path.Combine(install, "config", "config.xml");
        File.Copy(Legacy("java/config.xml"), java);
        File.SetLastWriteTimeUtc(java, new DateTime(2016, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        string godot = Path.Combine(install, "project.godot");
        File.WriteAllText(godot, File.ReadAllText(Legacy("godot/project.godot")).Replace("config/hide_ui=false", "config/hide_ui=true"));
        File.SetLastWriteTimeUtc(godot, new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        SaveStore first = SaveStore.Load(Locations(null, install));
        Assert.True(first.Settings.HideUi);
        Assert.False(first.Settings.MusicMuted);

        first.Settings.HideUi = false;
        first.SaveSettings();
        SaveStore second = SaveStore.Load(Locations(null, install));

        Assert.False(second.Settings.HideUi);
    }
}
