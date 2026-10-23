using System;
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

    // Set by the window layer: what the display settings do.
    public Action<Settings>? ApplyDisplay { get; set; }

    public bool QuitRequested { get; set; }

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
