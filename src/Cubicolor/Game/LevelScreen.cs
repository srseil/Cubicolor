using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cubicolor.Platform;
using Cubicolor.Render;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;

namespace Cubicolor.Game;

// Playing one level: the board, its name in the corner, the first levels' hints, and the pause and completion
// panels over it.
public sealed class LevelScreen : IScreen
{
    private enum Panel { None, Pause, Completed }

    private readonly GameContext _context;
    private readonly Puzzle _puzzle;
    private readonly BoardView _board;
    private Panel _panel;
    private double? _completeAt;

    public Level Level => _puzzle.Level;
    public Puzzle Puzzle => _puzzle;
    public BoardView Board => _board;
    public string State => _panel switch { Panel.Pause => "paused", Panel.Completed => "completed", _ => _board.Ready() ? "ready" : "busy" };

    public LevelScreen(GameContext context, Difficulty difficulty, int number)
    {
        _context = context;
        Level level = context.Load(difficulty, number);
        _puzzle = new Puzzle(level);
        _board = new BoardView(level, context.Meshes);
        _board.Start(_puzzle);
        PlayArrivals();
    }

    public IScreen Frame(double seconds, Scene scene)
    {
        _board.Advance(seconds);
        PlayDue();
        Ui ui = _context.Ui;
        IInput input = ui.Input;
        Settings settings = _context.Saves.Settings;
        IScreen next = this;

        if (_panel == Panel.None)
        {
            if (Pressed(input, settings.Keys[GameAction.Pause]) || input.Gamepad.WasPressed(GamepadButtons.Menu))
            {
                _panel = Panel.Pause;
                ui.Focus("Resume", force: true);
            }
            else if (Pressed(input, settings.Keys[GameAction.Restart]) || input.Gamepad.WasPressed(GamepadButtons.East))
            {
                Restart();
            }
            else if (!_puzzle.Solved && MoveInput(input, settings) is { } direction)
            {
                _queued = (direction, _board.Now);
            }
            if (_queued is { } queued && !_puzzle.Solved)
            {
                // A press during the last roll waits a moment for the cube, so quick play is not lost.
                if (_board.Now - queued.At > BoardView.Roll + 0.15) _queued = null;
                else if (_board.Ready(_puzzle.Position.Step(queued.Direction)))
                {
                    _queued = null;
                    Roll(queued.Direction);
                }
            }
        }
        else if (ui.Navigation.Back && _panel == Panel.Pause)
        {
            _panel = Panel.None;
        }

        if (_puzzle.Solved && _completeAt is { } at && _board.Now >= at && _panel == Panel.None)
        {
            _panel = Panel.Completed;
            ui.Focus(Level.Number < Simulation.Levels.Level.PerDifficulty ? "Next Level" : "Main Menu", force: true);
        }

        scene.Background = _context.Background;
        scene.Lighting = GameContext.Light;
        // The camera, fitted again only when the window changes shape.
        float aspect = ui.Size.X / ui.Size.Y;
        if (_camera == null || Math.Abs(_cameraAspect - aspect) > 1e-4f)
        {
            _cameraAspect = aspect;
            _camera = CameraFit.FitCentred(_board.Footprint(), _board.Extent(), aspect, 0.1f, 6f);
        }
        (scene.View, scene.Projection) = _camera.Value;
        _board.Draw(scene);

        DrawHud(ui, settings);

        // The pause and completion panels fade in and out over a sixth of a second; one fading out is drawn but
        // no longer takes input.
        if (_panel != Panel.None) _shown = _panel;
        _panelFade = Math.Clamp(_panelFade + (_panel != Panel.None ? 1 : -1) * (float) seconds / 0.16f, 0, 1);
        if (_panelFade <= 0) _shown = Panel.None;
        if (_shown != Panel.None)
        {
            ui.Overlay.Opacity = _panelFade * _panelFade * (3 - 2 * _panelFade);
            ui.Interactive = _panel != Panel.None;
            IScreen? chosen = _shown == Panel.Pause ? PausePanel(ui) : CompletedPanel(ui);
            if (ui.Interactive) next = chosen ?? next;
            ui.Overlay.Opacity = 1;
            ui.Interactive = true;
        }
        return next;
    }

    private static bool Pressed(IInput input, Key key) => key != Key.Unknown && input.Pressed(key);

    // A roll pressed on the player's keys, the d-pad or the stick. Each roll is its own press: holding a key
    // does not roll on, since every tile left behind is gone for good.
    private static Direction? MoveInput(IInput input, Settings settings)
    {
        GamepadState pad = input.Gamepad;
        (GameAction Action, GamepadButtons Pad, Direction Direction)[] moves =
        [
            (GameAction.Forwards, GamepadButtons.Up | GamepadButtons.StickUp, Direction.Forwards),
            (GameAction.Backwards, GamepadButtons.Down | GamepadButtons.StickDown, Direction.Backwards),
            (GameAction.Left, GamepadButtons.Left | GamepadButtons.StickLeft, Direction.Left),
            (GameAction.Right, GamepadButtons.Right | GamepadButtons.StickRight, Direction.Right),
        ];
        foreach (var (action, buttons, direction) in moves)
            if (Pressed(input, settings.Keys[action]) || pad.WasPressed(buttons))
                return direction;
        return null;
    }

    private (Direction Direction, double At)? _queued;
    private Panel _shown;
    private (System.Numerics.Matrix4x4, System.Numerics.Matrix4x4)? _camera;
    private float _cameraAspect;
    private float _panelFade;

    // Sounds waiting for their moment on the board's clock: what a roll causes is heard as the cube lands.
    private readonly List<(double At, Sound Sound)> _sounds = [];

    private void PlayAt(double at, Sound sound) => _sounds.Add((at, sound));

    // Each tile clicks into place with its own sound as the wave brings it up, as in the earlier versions;
    // tiles arriving together share one.
    private void PlayArrivals()
    {
        double last = double.NegativeInfinity;
        foreach (double at in _board.Arrivals.Order())
        {
            if (at - last < 0.04) continue;
            PlayAt(at, Sound.Revive);
            last = at;
        }
    }

    private void PlayDue()
    {
        for (int i = _sounds.Count - 1; i >= 0; i--)
            if (_board.Now >= _sounds[i].At)
            {
                _context.Audio.Play(_sounds[i].Sound);
                _sounds.RemoveAt(i);
            }
    }

    private void Roll(Direction direction)
    {
        Move move = _puzzle.Roll(direction);
        _board.Show(move);
        if (!move.Rolled) return;

        double landed = _board.Now + BoardView.Roll;
        PlayAt(landed, Sound.Step);
        if (move.PaintBefore != Paint.None && move.PaintAfter != move.PaintBefore)
        {
            PlayAt(landed, Sound.Unpaint);
            if (move.PaintAfter != Paint.None) PlayAt(landed + BoardView.PaintTime, Sound.Paint);
        }
        else if (move.PaintAfter != move.PaintBefore)
        {
            PlayAt(landed, Sound.Paint);
        }
        if (move.Solved) Solve(landed);
    }

    public void Restart()
    {
        if (_puzzle.Moves == 0 && !_puzzle.Solved) return;
        _puzzle.Restart();
        _board.Restart();
        _completeAt = null;
        _queued = null;
        _sounds.Clear();
        _panel = Panel.None;
        PlayArrivals();
    }

    private void Solve(double landed)
    {
        Progress progress = _context.Saves.Progress;
        progress.Complete(Level.Difficulty, Level.Number);
        _context.Saves.SaveProgress();
        if (Level.Number == Simulation.Levels.Level.PerDifficulty) _context.Achievements.Unlock(Level.Difficulty);
        PlayAt(landed, Sound.Solve);
        _completeAt = landed + 0.4;
    }

    private void DrawHud(Ui ui, Settings settings)
    {
        if (settings.HideUi) return;
        ui.Label(Level.Difficulty.ToString(), new Vector2(ui.Px(40), ui.Px(24)), 42);
        ui.Label($"Level {Level.Number}", new Vector2(ui.Px(42), ui.Px(76)), 30);

        if (Level.Difficulty != Difficulty.Normal || _panel != Panel.None) return;
        string[] hints = Level.Number switch
        {
            1 => ["1.  Color the cube", "2.  Match it with the colored tile", "3.  Get to the exit"],
            2 => ["You can reset the level by pressing the restart button."],
            3 => ["The order in which you match the different colors is irrelevant."],
            _ => [],
        };
        // Centred at the bottom of the window, whatever its shape.
        float line = ui.Px(36);
        float y = ui.Size.Y - ui.Px(40) - line * hints.Length;
        foreach (string hint in hints)
        {
            ui.Label(hint, new Vector2((ui.Size.X - ui.TextWidth(hint, 26)) / 2, y), 26);
            y += line;
        }
    }

    private IScreen? PausePanel(Ui ui)
    {
        Rect panel = CentredPanel(ui, 3);
        ui.Label("Pause", new Rect(panel.X, panel.Y + ui.Px(16), panel.Width, ui.Px(44)), 36);
        if (ui.Button("Resume", Row(ui, panel, 0))) _panel = Panel.None;
        if (ui.Button("Restart", Row(ui, panel, 1))) Restart();
        if (ui.Button("Main Menu", Row(ui, panel, 2))) return new MenuScreen(_context, Level.Difficulty);
        return null;
    }

    private IScreen? CompletedPanel(Ui ui)
    {
        bool last = Level.Number == Simulation.Levels.Level.PerDifficulty;
        Rect panel = CentredPanel(ui, last ? 2 : 3);
        ui.Label("Completed", new Rect(panel.X, panel.Y + ui.Px(16), panel.Width, ui.Px(44)), 36);
        int row = 0;
        if (!last && ui.Button("Next Level", Row(ui, panel, row++))) return new LevelScreen(_context, Level.Difficulty, Level.Number + 1);
        if (ui.Button("Play Again", Row(ui, panel, row++))) Restart();
        if (ui.Button("Main Menu", Row(ui, panel, row))) return new MenuScreen(_context, Level.Difficulty);
        return null;
    }

    private static Rect CentredPanel(Ui ui, int buttons)
    {
        float width = 300, height = 90 + buttons * 58;
        Rect panel = ui.R((Ui.CanvasWidth - width) / 2, (Ui.CanvasHeight - height) / 2, width, height);
        ui.Panel(panel);
        return panel;
    }

    private static Rect Row(Ui ui, Rect panel, int index) =>
        new(panel.X + ui.Px(30), panel.Y + ui.Px(76 + index * 58), panel.Width - ui.Px(60), ui.Px(46));
}
