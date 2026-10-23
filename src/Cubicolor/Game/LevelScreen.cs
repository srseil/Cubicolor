using System;
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
    }

    public IScreen Frame(double seconds, Scene scene)
    {
        _board.Advance(seconds);
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
                if (_board.Now - queued.At > 0.3) _queued = null;
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
        var (min, max) = _board.Bounds();
        (scene.View, scene.Projection) = CameraFit.Fit(min, max, ui.Size.X / ui.Size.Y, 0.1f, 6f);
        _board.Draw(scene);

        DrawHud(ui, settings);
        if (_panel == Panel.Pause) next = PausePanel(ui) ?? next;
        else if (_panel == Panel.Completed) next = CompletedPanel(ui) ?? next;
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

    private void Roll(Direction direction)
    {
        Move move = _puzzle.Roll(direction);
        _board.Show(move);
        if (!move.Rolled) return;

        _context.Audio.Play(Sound.Step);
        if (move.PaintAfter != Paint.None && move.PaintAfter != move.PaintBefore) _context.Audio.Play(Sound.Paint);
        else if (move.PaintBefore != Paint.None && move.PaintAfter == Paint.None) _context.Audio.Play(Sound.Unpaint);
        if (move.Solved) Solve();
    }

    public void Restart()
    {
        if (_puzzle.Moves == 0 && !_puzzle.Solved) return;
        _puzzle.Restart();
        _board.Restart();
        _completeAt = null;
        _queued = null;
        _panel = Panel.None;
        _context.Audio.Play(Sound.Revive);
    }

    private void Solve()
    {
        Progress progress = _context.Saves.Progress;
        progress.Complete(Level.Difficulty, Level.Number);
        _context.Saves.SaveProgress();
        if (Level.Number == Simulation.Levels.Level.PerDifficulty) _context.Achievements.Unlock(Level.Difficulty);
        _context.Audio.Play(Sound.Solve);
        _completeAt = _board.Now + BoardView.Roll + 0.4;
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
        float line = 36;
        float y = 720 - 40 - line * hints.Length;
        foreach (string hint in hints)
        {
            float width = ui.TextWidth(hint, 26) / ui.Scale;
            ui.Label(hint, new Vector2(ui.Px((ui.Size.X / ui.Scale - width) / 2), ui.Px(y)), 26);
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
        Vector2 size = ui.Size / ui.Scale;
        Rect panel = ui.R((size.X - width) / 2, (size.Y - height) / 2, width, height);
        ui.Panel(panel);
        return panel;
    }

    private static Rect Row(Ui ui, Rect panel, int index) =>
        new(panel.X + ui.Px(30), panel.Y + ui.Px(76 + index * 58), panel.Width - ui.Px(60), ui.Px(46));
}
