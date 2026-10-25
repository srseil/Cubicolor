using System;
using System.Collections.Generic;
using Cubicolor.Render;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;

namespace Cubicolor.Game;

public enum Sound
{
    Click,
    Step,
    Paint,
    Unpaint,
    Solve,
    Revive,
}

public interface IAudio : IDisposable
{
    void Play(Sound sound);
    // Applies the volume settings, music included.
    void Apply(Settings settings);
    // Once a frame: lets the output notice it has stopped and start again.
    void Watch();
}

// Where the mixed sound goes: the platform's audio output.
public interface IAudioOutput : IDisposable
{
    void Watch();
}

// Steam's achievements, one per difficulty finished; nothing without Steam.
public interface IAchievements
{
    void Unlock(Difficulty finished);
}

public sealed class NoAchievements : IAchievements
{
    public void Unlock(Difficulty finished) { }
}

// What the screens share: passed down from the composition root, never reached through a global.
public sealed class GameContext(IRenderer renderer, BoardMeshes meshes, Ui ui, SaveStore saves, IAudio audio, IAchievements achievements, string levels)
{
    public IRenderer Renderer { get; } = renderer;
    public BoardMeshes Meshes { get; } = meshes;
    public Ui Ui { get; } = ui;
    public SaveStore Saves { get; } = saves;
    public IAudio Audio { get; } = audio;
    public IAchievements Achievements { get; } = achievements;
    public string LevelsDirectory { get; } = levels;

    // Set by the window layer: what the display settings do, and opening a web page in the browser.
    public Action<Settings>? ApplyDisplay { get; set; }
    public Action<string>? OpenUrl { get; set; }

    public bool QuitRequested { get; set; }

    // The language: forced for a run (--language), else the player's choice, else Steam's or the system's.
    public string LanguageDirectory { get; init; } = "";
    public Language? ForcedLanguage { get; init; }
    public Language DetectedLanguage { get; init; } = Language.English;

    public Language Language => ForcedLanguage ?? Saves.Settings.Language ?? DetectedLanguage;

    // Puts the UI's text into the current language, when that changed.
    public void ApplyLanguage()
    {
        if (Ui.Strings.Language == Language && Ui.Strings != Strings.English) return;
        Ui.Strings = Strings.Load(LanguageDirectory, Language);
        Ui.FallbackFonts = FallbackFonts(Language);
    }

    // The fonts that draw what Vollkorn can't: Noto Serif CJK, trimmed to the characters the game shows
    // (tools/subset-fonts.sh). The language's own comes first, so the characters the scripts share (Han) take
    // its forms; the others still draw the names in the language list.
    public static IReadOnlyList<string> FallbackFonts(Language language)
    {
        const string chinese = "NotoSerifSC-Medium-Subset.otf", japanese = "NotoSerifJP-Medium-Subset.otf", korean = "NotoSerifKR-Medium-Subset.otf";
        return language switch
        {
            Language.Japanese => [japanese, chinese, korean],
            Language.Korean => [korean, chinese, japanese],
            _ => [chinese, japanese, korean],
        };
    }

    public Level Load(Difficulty difficulty, int number) => LevelFile.Load(LevelsDirectory, difficulty, number);

    // Lighting tuned so the board's faces match the earlier versions' screenshots.
    public static readonly Lighting Light = new(new(0.27f, 1f, 0.6f), new(0.233f), new(0.574f), 0.09f, 8f);

    public System.Numerics.Vector4 Background => Saves.Settings.DarkBackground ? Palette.DarkBackground : Palette.Background;
}

public interface IScreen
{
    // One frame: read input (through the context's Ui), advance animations, and draw into the scene and the
    // overlay. Returns the screen to show next frame (itself, usually).
    IScreen Frame(double seconds, Scene scene);
}
