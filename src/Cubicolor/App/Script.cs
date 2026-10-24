using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cubicolor.Game;
using Cubicolor.Platform;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;
using Cubicolor.Utilities;

namespace Cubicolor.App;

// A headless run's commands, one per line or separated by ';', `#` starting a comment:
//
//   wait <seconds>              let game time pass
//   wait ready                  until the level takes input (or fail after 10 s)
//   press <Key>                 a key for one frame (Key enum names: Up, Return, Escape, R, ...)
//   pad <button>                a controller button for one frame (South, East, Menu, Up, ...)
//   click "<label>"             the widget with that label, as the mouse would
//   moves <FBLR...>             rolls, each once the cube is ready
//   play <level>                straight into a level, e.g. play genius07
//   screenshot <path>           the next frame as a PNG
//   expect <what> <value>       fail unless it holds: screen (menu|level), level (normal03), state
//                               (ready|busy|paused|completed), solved.<difficulty> (a number), focus
//
// A failing line ends the run with an error naming it.
internal sealed class Script
{
    private readonly Queue<string> _lines;
    private readonly ScriptedInput _input;
    private double _waitUntil = double.NegativeInfinity;
    private double _deadline = double.PositiveInfinity;
    private string? _pendingScreenshot;
    private Queue<char>? _moves;

    public string? Error { get; private set; }

    // A screen the script switches to before the next frame.
    public IScreen? Next { get; set; }
    public bool Done => _lines.Count == 0 && _pendingScreenshot == null && _moves == null && Error == null && _time >= _waitUntil;

    private double _time;

    public Script(string text, ScriptedInput input)
    {
        _input = input;
        _lines = new Queue<string>(text.Split(['\n', ';'])
            .Select(l => (l.IndexOf('#') is var c && c >= 0 ? l[..c] : l).Trim())
            .Where(l => l.Length > 0));
    }

    // Before a frame: queue this frame's input. `screen` is what is showing.
    public void BeforeFrame(double time, IScreen screen, GameContext context)
    {
        _time = time;
        if (Error != null) return;
        if (time < _waitUntil) return;

        if (_moves != null)
        {
            if (screen is not LevelScreen level) { Fail("moves outside a level"); return; }
            if (time > _deadline) { Fail("the cube never became ready"); return; }
            if (!level.Board.Ready(level.Puzzle.Position.Step(DirectionOf(_moves.Peek())))) return;
            _input.Press(KeyFor(DirectionOf(_moves.Dequeue()), context));
            if (_moves.Count == 0) _moves = null;
            // Give the press a frame to land before checking readiness again.
            _waitUntil = time + 1.0 / 30;
            return;
        }

        while (_lines.Count > 0 && Error == null && time >= _waitUntil && _moves == null && _pendingScreenshot == null)
        {
            string line = _lines.Peek();
            if (!Step(line, time, screen, context)) return;   // still waiting on this line
            if (_lines.Count > 0) _lines.Dequeue();   // a failure has emptied the queue already
        }
    }

    // After a frame has been drawn: take the screenshot asked for.
    public void AfterFrame(Render.IRenderer renderer)
    {
        if (_pendingScreenshot is not { } path) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Png.Write(path, renderer.ReadPixels(), renderer.Width, renderer.Height, bgra: false, opaque: true);
        Log.Info($"Screenshot: {path}");
        _pendingScreenshot = null;
    }

    private bool Step(string line, double time, IScreen screen, GameContext context)
    {
        string[] words = line.Split(' ', 2, StringSplitOptions.TrimEntries);
        string argument = words.Length > 1 ? words[1] : "";
        switch (words[0])
        {
            case "wait" when argument == "ready":
                if (double.IsPositiveInfinity(_deadline)) _deadline = time + 10;
                if (screen is LevelScreen { State: "ready" }) { _deadline = double.PositiveInfinity; return true; }
                if (time > _deadline) Fail(line);
                return false;
            case "wait":
                _waitUntil = time + double.Parse(argument, CultureInfo.InvariantCulture);
                return true;
            case "press":
                if (!Enum.TryParse(argument, out Key key)) { Fail(line); return true; }
                _input.Press(key);
                _waitUntil = time + 1.0 / 30;
                return true;
            case "pad":
                if (!Enum.TryParse(argument, out GamepadButtons button)) { Fail(line); return true; }
                _input.Press(button);
                _waitUntil = time + 1.0 / 30;
                return true;
            case "click":
                string label = argument.Trim('"');
                var widget = context.Ui.Widgets.FirstOrDefault(w => w.Label == label);
                if (widget.Label == null)
                {
                    Fail($"{line}: on screen are {string.Join(", ", context.Ui.Widgets.Select(w => $"\"{w.Label}\""))}");
                    return true;
                }
                _input.Click(widget.Rect.Centre);
                _waitUntil = time + 1.0 / 30;
                return true;
            case "moves":
                _moves = new Queue<char>(argument);
                _deadline = time + 10 + argument.Length;
                return true;
            case "play":
                if (argument.Length < 3 || !Enum.TryParse(argument[..^2], true, out Difficulty difficulty)
                    || !int.TryParse(argument[^2..], out int number)) { Fail(line); return true; }
                Next = new LevelScreen(context, difficulty, number);
                return true;
            case "screenshot":
                _pendingScreenshot = argument;
                return true;
            case "expect":
                string[] parts = argument.Split(' ', 2, StringSplitOptions.TrimEntries);
                string actual = Read(parts[0], screen, context);
                if (parts.Length < 2 || actual != parts[1]) Fail($"{line} (it is {actual})");
                return true;
            default:
                Fail($"unknown command: {line}");
                return true;
        }
    }

    private static string Read(string what, IScreen screen, GameContext context) => what switch
    {
        "screen" => screen is LevelScreen ? "level" : "menu",
        "level" => screen is LevelScreen l ? Simulation.Levels.LevelFile.FileName(l.Level.Difficulty, l.Level.Number)[..^4] : "none",
        "state" => screen is LevelScreen l ? l.State : "menu",
        "focus" => context.Ui.Focused ?? "none",
        _ when what.StartsWith("solved.", StringComparison.Ordinal) && Enum.TryParse(what[7..], true, out Difficulty d)
            => context.Saves.Progress.Solved(d).ToString(CultureInfo.InvariantCulture),
        _ => $"(unknown: {what})",
    };

    private static Direction DirectionOf(char c) => c switch
    {
        'F' => Direction.Forwards,
        'B' => Direction.Backwards,
        'L' => Direction.Left,
        _ => Direction.Right,
    };

    private static Key KeyFor(Direction direction, GameContext context) => context.Saves.Settings.Keys[direction switch
    {
        Direction.Forwards => Simulation.Saves.GameAction.Forwards,
        Direction.Backwards => Simulation.Saves.GameAction.Backwards,
        Direction.Left => Simulation.Saves.GameAction.Left,
        _ => Simulation.Saves.GameAction.Right,
    }];

    private void Fail(string message)
    {
        Error ??= message;
        _lines.Clear();
        _moves = null;
    }
}
