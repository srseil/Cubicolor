using System;
using System.Linq;
using System.Numerics;
using Cubicolor.Platform;
using Cubicolor.Render;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;

namespace Cubicolor.Game;

// The main menu: the logo, the sections down the left (Play, Controls, Settings, Credits, Quit) and the
// chosen section's panel on the right, as in both earlier versions.
public sealed class MenuScreen : IScreen
{
    public const string Version = "v2.0";

    private enum Section { Play, Controls, Settings, Credits }

    private readonly GameContext _context;
    private readonly LogoView _logo;
    private Section _section = Section.Play, _sectionShown = Section.Play;
    private float _sectionFade = 1;
    private Difficulty _difficulty;
    private (GameAction Action, bool Pad)? _rebinding;

    public MenuScreen(GameContext context, Difficulty difficulty = Difficulty.Normal)
    {
        _context = context;
        _difficulty = difficulty;
        _logo = new LogoView(context.Meshes);
        context.Ui.Focus("Play", force: true);
    }

    // Shows a key's name as the keyboard layout prints it; set by the platform.
    public static Func<Key, string> KeyName { get; set; } = key => key.ToString();

    public IScreen Frame(double seconds, Scene scene)
    {
        Ui ui = _context.Ui;
        scene.Background = _context.Background;
        scene.Lighting = GameContext.Light;
        const float width = Ui.CanvasWidth;
        const float left = (width - 1060) / 2;

        ui.Label("Cubicolor", ui.R(0, 28, width, 100), 84, style: FontStyle.Black);
        _previewCooldown -= seconds;

        if (_rebinding is { } rebinding)
        {
            // The press that sets a binding must not also move the focus or press a button.
            ui.Hold();
            Capture(ui, rebinding.Action, rebinding.Pad);
        }
        else if (ui.Navigation.Back && _section != Section.Play)
        {
            ui.Focus(_section.ToString(), force: true);
            _section = Section.Play;
        }

        // Right from the sections enters the panel at its top; left from the panel goes back to its section.
        string[] sections = ["Play", "Controls", "Settings", "Credits", "Quit"];
        bool fromSections = ui.FocusBefore != null && sections.Contains(ui.FocusBefore);
        if (ui.Navigation.Move == Direction.Right && fromSections && FirstInPanel() is { } first) ui.Focus(first, force: true);
        else if (ui.Navigation.Move == Direction.Left && !fromSections && ui.Focused != null && sections.Contains(ui.Focused))
            ui.Focus(_section.ToString(), force: true);

        IScreen next = this;
        (string Label, Section? Section)[] items =
            [("Play", Section.Play), ("Controls", Section.Controls), ("Settings", Section.Settings), ("Credits", Section.Credits), ("Quit", null)];
        for (int i = 0; i < items.Length; i++)
        {
            var (label, section) = items[i];
            if (ui.Button(label, ui.R(left, 270 + i * 62, 210, 48), selected: section == _section && section != null))
            {
                if (section is { } s) _section = s;
                else _context.QuitRequested = true;
            }
        }

        Rect panel = ui.R(left + 260, 262, 800, 420);
        ui.Panel(panel, under: true);
        // A section's contents fade in when it is chosen.
        if (_section != _sectionShown) (_sectionShown, _sectionFade) = (_section, 0);
        _sectionFade = Math.Min(1, _sectionFade + (float) seconds / 0.15f);
        ui.Overlay.Opacity = _sectionFade;
        switch (_section)
        {
            case Section.Play: next = Play(ui, panel) ?? next; break;
            case Section.Controls: Controls(ui, panel); break;
            case Section.Settings: SettingsPanel(ui, panel); break;
            case Section.Credits: Credits(ui, panel, scene, seconds); break;
        }
        ui.Overlay.Opacity = 1;

        ui.Label(Version, ui.P(width - ui.TextWidth(Version, 18) / ui.Scale - 20, 690), 18);
        if (ui.Clicked) _context.Audio.Play(Sound.Click);
        return next;
    }

    private string? FirstInPanel() => _section switch
    {
        Section.Play => _difficulty.ToString(),
        Section.Controls => "Forwards key",
        Section.Settings => "Dark Background",
        _ => null,
    };

    private IScreen? Play(Ui ui, Rect panel)
    {
        Progress progress = _context.Saves.Progress;
        float x = ui.DesignX(panel.X), y = ui.DesignY(panel.Y);
        Difficulty[] all = Enum.GetValues<Difficulty>();
        for (int i = 0; i < all.Length; i++)
        {
            Difficulty d = all[i];
            bool open = progress.Solved(d) != Progress.Locked;
            if (ui.Button(d.ToString(), ui.R(x + 160 + i * 165, y + 24, 150, 44), enabled: open, selected: d == _difficulty)) _difficulty = d;
        }
        for (int n = 1; n <= Level.PerDifficulty; n++)
        {
            int row = (n - 1) / 4, column = (n - 1) % 4;
            Rect cell = ui.R(x + 250 + column * 80, y + 92 + row * 78, 64, 64);
            bool open = progress.IsOpen(_difficulty, n);
            // The cells answer to the mouse right up to their neighbours, so the grid has no dead space.
            Rect hit = ui.R(x + 242 + column * 80, y + 85 + row * 78, 80, 78);
            if (ui.Button(n.ToString(), cell, 26, enabled: open, done: progress.IsSolved(_difficulty, n), id: $"{_difficulty} {n}", hit: hit))
                return new LevelScreen(_context, _difficulty, n);
        }
        return null;
    }

    // Each action's key and controller button, side by side. A controller's buttons show as the controller's
    // own symbols and can be changed while one is connected; without one, they are named in text.
    private void Controls(Ui ui, Rect panel)
    {
        Settings settings = _context.Saves.Settings;
        bool connected = ui.Input.Gamepad.Connected;
        float x = ui.DesignX(panel.X) + 60, y = ui.DesignY(panel.Y) + 24;
        ui.Label("Keyboard", ui.R(x + 220, y, 200, 30), 24);
        ui.Label("Controller", ui.R(x + 460, y, 200, 30), 24);
        (GameAction Action, string Label)[] rows =
        [
            (GameAction.Forwards, "Forwards"), (GameAction.Backwards, "Backwards"), (GameAction.Left, "Left"), (GameAction.Right, "Right"),
            (GameAction.Restart, "Restart"), (GameAction.Pause, "Pause Menu"),
        ];
        float rowY = y + 44;
        for (int i = 0; i < rows.Length; i++)
        {
            var (action, label) = rows[i];
            if (i == 4) rowY += 14;
            ui.Label(label, ui.P(x + 20, rowY + 8), 24);
            string key = _rebinding == (action, false) ? "Press a key" : KeyName(settings.Keys[action]);
            if (ui.Button(key, ui.R(x + 220, rowY, 200, 40), 22, id: $"{label} key")) _rebinding = (action, false);
            GamepadButtons button = Flag(settings.Pad[action]);
            bool capturing = _rebinding == (action, true);
            if (ui.SymbolButton($"{label} button", ui.R(x + 460, rowY, 200, 40), capturing ? [] : ui.Input.SymbolsFor(button),
                                capturing ? "Press a button" : PadName(settings.Pad[action]), enabled: connected))
                _rebinding = (action, true);
            rowY += 50;
        }
    }

    private static GamepadButtons Flag(PadButton button) => (GamepadButtons) (1 << (int) button);

    // A controller button's name in the Xbox layout's letters, for when no symbol is there to show.
    private static string PadName(PadButton button) => button switch
    {
        PadButton.South => "A",
        PadButton.East => "B",
        PadButton.West => "X",
        PadButton.North => "Y",
        PadButton.LeftShoulder => "LB",
        PadButton.RightShoulder => "RB",
        PadButton.Menu => "Menu",
        PadButton.Options => "View",
        PadButton.Up => "D-Pad Up",
        PadButton.Down => "D-Pad Down",
        PadButton.Left => "D-Pad Left",
        _ => "D-Pad Right",
    };

    // The next key or controller button pressed becomes the binding. One already bound to another action swaps
    // with this one, so no action is ever left without one. Escape leaves a controller binding as it was.
    private void Capture(Ui ui, GameAction action, bool pad)
    {
        Settings settings = _context.Saves.Settings;
        if (pad)
        {
            if (ui.Input.Pressed(Key.Escape) || !ui.Input.Gamepad.Connected)
            {
                _rebinding = null;
                return;
            }
            GamepadButtons pressed = ui.Input.Gamepad.Pressed;
            PadButton? chosen = null;
            foreach (PadButton b in Enum.GetValues<PadButton>())
                if ((pressed & Flag(b)) != 0) { chosen = b; break; }
            if (chosen is not { } button) return;
            foreach (var (other, bound) in settings.Pad.ToArray())
                if (bound == button && other != action) settings.Pad[other] = settings.Pad[action];
            settings.Pad[action] = button;
        }
        else
        {
            Key key = ui.Input.PressedThisFrame().FirstOrDefault(k => k != Key.Unknown);
            if (key == Key.Unknown) return;
            foreach (var (other, bound) in settings.Keys.ToArray())
                if (bound == key && other != action) settings.Keys[other] = settings.Keys[action];
            settings.Keys[action] = key;
        }
        _context.Saves.SaveSettings();
        _rebinding = null;
    }

    // One column, top to bottom, so up and down move between settings and left and right are free for the
    // sliders.
    private void SettingsPanel(Ui ui, Rect panel)
    {
        Settings s = _context.Saves.Settings;
        float x = ui.DesignX(panel.X) + 70, y = ui.DesignY(panel.Y) + 22;
        const float row = 36, gap = 12, width = 560;
        bool changed = false, display = false;

        bool dark = s.DarkBackground, show = !s.HideUi;
        changed |= ui.Toggle("Dark Background", ui.R(x, y, width, 32), ref dark);
        changed |= ui.Toggle("Show Level Name and Tutorial", ui.R(x, y += row, width, 32), ref show);
        (s.DarkBackground, s.HideUi) = (dark, !show);

        double music = s.MusicVolume, sound = s.SoundVolume;
        bool musicMuted = s.MusicMuted, soundMuted = s.SoundMuted;
        changed |= Volume(ui, "Music", x, y += row + gap, ref music, ref musicMuted);
        bool soundChanged = Volume(ui, "Sound", x, y += 2 * row + 8 + gap, ref sound, ref soundMuted);
        changed |= soundChanged;
        (s.MusicVolume, s.SoundVolume, s.MusicMuted, s.SoundMuted) = (music, sound, musicMuted, soundMuted);

        bool fullscreen = s.Fullscreen, vsync = s.VSync;
        y += 2 * row + 8 + gap * 2;
        display |= ui.Toggle("Fullscreen", ui.R(x, y, width, 32), ref fullscreen);
        display |= ui.Toggle("VSync", ui.R(x, y + row, width, 32), ref vsync);
        (s.Fullscreen, s.VSync) = (fullscreen, vsync);

        if (changed || display)
        {
            _context.Saves.SaveSettings();
            _context.Audio.Apply(s);
            ui.Dark = s.DarkBackground;
        }
        // A taste of the new sound volume as it changes, a few times a second at most.
        if (soundChanged && _previewCooldown <= 0)
        {
            _context.Audio.Play(Sound.Step);
            _previewCooldown = 0.15;
        }
        if (display) _context.ApplyDisplay?.Invoke(s);
    }

    private double _previewCooldown;

    // A switch for the music or the sounds, and their volume beneath it, which can only be changed while
    // they are on.
    private static bool Volume(Ui ui, string name, float x, float y, ref double volume, ref bool muted)
    {
        bool on = !muted;
        bool changed = ui.Toggle(name, ui.R(x, y, 560, 32), ref on);
        muted = !on;
        changed |= ui.Slider($"{name} Volume", ui.R(x + 44, y + 46, 380, 20), ref volume, ui.R(x, y + 38, 560, 36), enabled: on);
        ui.Label($"{Math.Round(volume * 100):0} %", ui.P(x + 444, y + 40), 22, on ? null : Style.DisabledText);
        return changed;
    }

    private void Credits(Ui ui, Rect panel, Scene scene, double seconds)
    {
        float x = ui.DesignX(panel.X), y = ui.DesignY(panel.Y);
        float w = panel.Width / ui.Scale;
        ui.Label("Cubicolor", ui.R(x, y + 18, w, 60), 48, style: FontStyle.Black);
        _logo.Advance(seconds);
        _logo.Draw(scene, ui.R(x + w / 2 - 130, y + 82, 260, 76), ui.Size, _sectionFade);
        ui.Label("A game by Stefan Seil", ui.R(x, y + 168, w, 36), 26);
        ui.Label("Relaxing Piano Music by Kevin MacLeod (incompetech.com)", ui.R(x, y + 222, w, 24), 18);
        ui.Label("Licensed under Creative Commons: By Attribution 3.0 License", ui.R(x, y + 246, w, 24), 18);
        ui.Label("https://creativecommons.org/licenses/by/3.0/", ui.R(x, y + 270, w, 24), 18);
        ui.Label("Vollkorn by Friedrich Althausen, SIL Open Font License 1.1", ui.R(x, y + 306, w, 24), 18);
        float linkWidth = ui.TextWidth(Website, 24) / ui.Scale + 16;
        if (ui.Link(Website, ui.R(x + (w - linkWidth) / 2, y + 352, linkWidth, 34), 24)) _context.OpenUrl?.Invoke("https://" + Website);
    }

    private const string Website = "srseil.com";
}
