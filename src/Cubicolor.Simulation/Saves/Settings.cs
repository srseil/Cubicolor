using System;
using System.Collections.Generic;
using Cubicolor.Simulation.Input;

namespace Cubicolor.Simulation.Saves;

// What a player can bind to a key.
public enum GameAction
{
    Forwards,
    Backwards,
    Left,
    Right,
    Restart,
    Pause,
}

// The player's choices in the settings and controls menus. Defaults are the earlier versions' defaults.
public sealed class Settings
{
    public bool DarkBackground { get; set; }
    public bool HideUi { get; set; }
    public double MusicVolume { get; set; } = 1.0;    // 0..1
    public double SoundVolume { get; set; } = 1.0;    // 0..1
    public bool MusicMuted { get; set; }
    public bool SoundMuted { get; set; }
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;
    // The window's size in points when not fullscreen, or null for a size picked from the screen.
    public (int Width, int Height)? WindowSize { get; set; }

    public Dictionary<GameAction, Key> Keys { get; } = DefaultKeys();
    public Dictionary<GameAction, PadButton> Pad { get; } = DefaultPad();

    // On a controller the d-pad moves (the left stick always does too), B restarts and Menu pauses, as the
    // earlier versions had it.
    public static Dictionary<GameAction, PadButton> DefaultPad() => new()
    {
        [GameAction.Forwards] = PadButton.Up,
        [GameAction.Backwards] = PadButton.Down,
        [GameAction.Left] = PadButton.Left,
        [GameAction.Right] = PadButton.Right,
        [GameAction.Restart] = PadButton.East,
        [GameAction.Pause] = PadButton.Menu,
    };

    public static Dictionary<GameAction, Key> DefaultKeys() => new()
    {
        [GameAction.Forwards] = Key.Up,
        [GameAction.Backwards] = Key.Down,
        [GameAction.Left] = Key.Left,
        [GameAction.Right] = Key.Right,
        [GameAction.Restart] = Key.Backspace,
        [GameAction.Pause] = Key.Escape,
    };

    public static double ClampVolume(double volume) => double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 1.0;
}
