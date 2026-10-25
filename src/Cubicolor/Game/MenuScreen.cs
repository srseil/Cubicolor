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
    public const string Version = "Cubicolor v2.0";

    private enum Section { Play, Controls, Settings, Credits }

    private readonly GameContext _context;
    private readonly LogoView _logo;
    private Section _section = Section.Play, _sectionShown = Section.Play;
    private float _sectionFade = 1;
    private Difficulty _difficulty;
    private (GameAction Action, bool Pad)? _rebinding;

    // Back from a level, the menu opens on Play with that level's difficulty chosen.
    public MenuScreen(GameContext context, Difficulty difficulty = Difficulty.Normal)
    {
        _context = context;
        _difficulty = difficulty;
        _logo = new LogoView(context.Meshes);
        context.Ui.Focus("Play", force: true);
    }

    // When the game starts, the menu opens on the Credits, as the earlier versions did.
    public static MenuScreen AtStart(GameContext context)
    {
        var menu = new MenuScreen(context) { _section = Section.Credits, _sectionShown = Section.Credits };
        context.Ui.Focus("Credits", force: true);
        return menu;
    }

    // Shows a key's name as the keyboard layout prints it, and the symbol for one that has a symbol rather
    // than a letter (arrows, return, ...); set by the platform.
    public static Func<Key, string> KeyName { get; set; } = key => key.ToString();
    public static Func<Key, string?> KeySymbol { get; set; } = _ => null;

    public IScreen Frame(double seconds, Scene scene)
    {
        Ui ui = _context.Ui;
        scene.Background = _context.Background;
        scene.Lighting = GameContext.Light;
        const float width = Ui.CanvasWidth;
        const float left = (width - SectionsWidth - 50 - PanelWidth) / 2;

        _previewCooldown -= seconds;

        if (_rebinding is { } rebinding)
        {
            // The press that sets a binding must not also move the focus or press a button.
            ui.Hold();
            Capture(ui, rebinding.Action, rebinding.Pad);
        }
        else if (ui.Navigation.Back && _section != Section.Play)
        {
            ui.Focus("Play", force: true);
            _section = Section.Play;
        }

        // Right from the sections enters the panel at its top; left from the panel goes back to its section.
        string[] sections = ["Play", "Controls", "Settings", "Credits", "Quit"];
        bool fromSections = ui.FocusBefore != null && sections.Contains(ui.FocusBefore);
        // The press that moves focus across is spent on that: it doesn't also step the widget it lands on.
        if (ui.Navigation.Move == Direction.Right && fromSections && FirstInPanel() is { } first)
        {
            ui.Focus(first, force: true);
            ui.Spend();
        }
        else if (ui.Navigation.Move == Direction.Left && !fromSections && ui.Focused != null && sections.Contains(ui.Focused))
        {
            ui.Focus(_section.ToString(), force: true);
            ui.Spend();
        }

        IScreen next = this;
        (string Label, Section? Section)[] items =
            [("Play", Section.Play), ("Controls", Section.Controls), ("Settings", Section.Settings), ("Credits", Section.Credits), ("Quit", null)];
        for (int i = 0; i < items.Length; i++)
        {
            var (label, section) = items[i];
            if (ui.Button(label, ui.R(left, SectionsTop + i * 62, SectionsWidth, 48), selected: section == _section && section != null))
            {
                if (section is { } s) _section = s;
                else _context.QuitRequested = true;
            }
        }

        Rect panel = ui.R(left + SectionsWidth + 50, PanelTop, PanelWidth, PanelHeight);
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

    // The section buttons and the panel, each centred on the canvas on its own.
    private const float SectionsWidth = 210, PanelWidth = 640, PanelHeight = 420, PanelTop = (Ui.CanvasHeight - PanelHeight) / 2;
    private const float SectionsTop = (Ui.CanvasHeight - (5 * 62 - 14)) / 2;

    // Settings and Controls stand on one grid of rows, from the same top with the same pitch, so their rows line
    // up as the panel switches between them; a gap sets groups apart.
    private const float Top = 24, Gap = 14, RowHeight = 36;

    // Both have seven rows, spread out to fill the panel, with the gap before the last two.
    private const float Pitch = (PanelHeight - 2 * Top - RowHeight - Gap) / 6;

    private static float Row(float panelY, int row) => panelY + Top + row * Pitch + (row >= 5 ? Gap : 0);

    private string? FirstInPanel() => _section switch
    {
        Section.Play => _difficulty.ToString(),
        Section.Controls => "Forwards key",
        Section.Settings => "Language",
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
            if (ui.Button(d.ToString(), ui.R(x + 80 + i * 165, y + 24, 150, 44), enabled: open, selected: d == _difficulty)) _difficulty = d;
        }
        for (int n = 1; n <= Level.PerDifficulty; n++)
        {
            int row = (n - 1) / 4, column = (n - 1) % 4;
            Rect cell = ui.R(x + 168 + column * 80, y + 92 + row * 78, 64, 64);
            bool open = progress.IsOpen(_difficulty, n);
            // The cells answer to the mouse right up to their neighbours, so the grid has no dead space.
            Rect hit = ui.R(x + 160 + column * 80, y + 85 + row * 78, 80, 78);
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
        float x = ui.DesignX(panel.X) + 45, y = ui.DesignY(panel.Y);
        ui.Label("Keyboard", ui.R(x + 170, Row(y, 0), 180, RowHeight), 24);
        ui.Label("Controller", ui.R(x + 370, Row(y, 0), 180, RowHeight), 24);
        (GameAction Action, string Label)[] rows =
        [
            (GameAction.Forwards, "Forwards"), (GameAction.Backwards, "Backwards"), (GameAction.Left, "Left"), (GameAction.Right, "Right"),
            (GameAction.Restart, "Restart"), (GameAction.Pause, "Pause Menu"),
        ];
        for (int i = 0; i < rows.Length; i++)
        {
            var (action, label) = rows[i];
            float rowY = Row(y, i + 1);
            ui.Label(label, ui.R(x, rowY, 160, RowHeight), 24, align: Align.Left);
            bool typing = _rebinding == (action, false);
            Key key = settings.Keys[action];
            string[] keySymbol = !typing && KeySymbol(key) is { } symbol ? [symbol] : [];
            if (ui.SymbolButton($"{label} key", ui.R(x + 170, rowY, 180, RowHeight), keySymbol, typing ? "Press a key" : KeyName(key)))
                _rebinding = (action, false);
            GamepadButtons button = Flag(settings.Pad[action]);
            bool capturing = _rebinding == (action, true);
            if (ui.SymbolButton($"{label} button", ui.R(x + 370, rowY, 180, RowHeight), capturing ? [] : ui.Input.SymbolsFor(button),
                                capturing ? "Press a button" : PadName(settings.Pad[action]), enabled: connected))
                _rebinding = (action, true);
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
        float x = ui.DesignX(panel.X) + 80, y = ui.DesignY(panel.Y);
        const float width = PanelWidth - 160;
        bool changed = false, display = false;

        // Each language by its own name; choosing one keeps it, whatever Steam or the system say.
        int language = (int) _context.Language;
        if (ui.Choice("Language", ui.R(x, Row(y, 0), width, RowHeight), ui.Px(width - 210), ref language, Languages.Count,
                      i => ((Language) i).NativeName()))
        {
            s.Language = (Language) language;
            changed = true;
            _context.ApplyLanguage();
        }

        bool dark = s.DarkBackground, show = !s.HideUi;
        changed |= ui.Toggle("Dark Background", ui.R(x, Row(y, 1), width, RowHeight), ref dark);
        changed |= ui.Toggle("Show Level Name and Tutorial", ui.R(x, Row(y, 2), width, RowHeight), ref show);
        (s.DarkBackground, s.HideUi) = (dark, !show);

        double music = s.MusicVolume, sound = s.SoundVolume;
        bool musicMuted = s.MusicMuted, soundMuted = s.SoundMuted;
        changed |= Volume(ui, "Music", x, Row(y, 3), width, ref music, ref musicMuted);
        bool soundChanged = Volume(ui, "Sound", x, Row(y, 4), width, ref sound, ref soundMuted);
        changed |= soundChanged;
        (s.MusicVolume, s.SoundVolume, s.MusicMuted, s.SoundMuted) = (music, sound, musicMuted, soundMuted);

        bool fullscreen = s.Fullscreen, vsync = s.VSync;
        display |= ui.Toggle("Fullscreen", ui.R(x, Row(y, 5), width, RowHeight), ref fullscreen);
        display |= ui.Toggle("VSync", ui.R(x, Row(y, 6), width, RowHeight), ref vsync);
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

    // A switch for the music or the sounds with their volume beside it, which can only be changed while they
    // are on.
    private static bool Volume(Ui ui, string name, float x, float y, float width, ref double volume, ref bool muted)
    {
        bool on = !muted;
        var (switched, moved) = ui.ToggleSlider(name, ui.R(x, y, width, RowHeight), ui.R(x + 210, y + 13, width - 290, 10), ref on, ref volume);
        muted = !on;
        ui.Label($"{Math.Round(volume * 100):0} %", ui.R(x + width - 64, y, 64, RowHeight), 22, on ? null : Style.DisabledText, align: Align.Right);
        return switched || moved;
    }

    // The credits as a column of blocks, centred in the panel: the title and the logo scene under it, then
    // paragraphs set apart by one gap, their lines evenly spaced, and the link last.
    private void Credits(Ui ui, Rect panel, Scene scene, double seconds)
    {
        const float paragraph = 18, line = 22, small = 18;
        string[] music = ["Relaxing Piano Music by Kevin MacLeod (incompetech.com)",
                          "Licensed under Creative Commons: By Attribution 3.0 License",
                          "https://creativecommons.org/licenses/by/3.0/"];
        string[] font = ["Vollkorn by Friedrich Althausen", "SIL Open Font License 1.1"];
        const float title = 56, logo = 84, author = 32, link = 32;
        float height = title + 4 + logo + paragraph + author + paragraph + music.Length * line + paragraph + font.Length * line + paragraph + link;

        float x = ui.DesignX(panel.X), w = PanelWidth;
        float y = ui.DesignY(panel.Y) + (PanelHeight - height) / 2;
        ui.Label("Cubicolor", ui.R(x, y, w, title), 48, style: FontStyle.Black);
        y += title + 4;
        _logo.Advance(seconds);
        _logo.Draw(scene, ui.R(x + w / 2 - 150, y, 300, logo), ui.Size, _sectionFade);
        y += logo + paragraph;
        ui.Label("A game by Stefan Seil", ui.R(x, y, w, author), 26);
        y += author + paragraph;
        foreach (string text in music)
        {
            ui.Label(text, ui.R(x, y, w, line), small);
            y += line;
        }
        y += paragraph;
        foreach (string text in font)
        {
            ui.Label(text, ui.R(x, y, w, line), small);
            y += line;
        }
        y += paragraph;
        float linkWidth = ui.TextWidth(Website, 24) / ui.Scale + 16;
        if (ui.Link(Website, ui.R(x + (w - linkWidth) / 2, y, linkWidth, link), 24)) _context.OpenUrl?.Invoke("https://" + Website);
    }

    private const string Website = "srseil.com";
}
