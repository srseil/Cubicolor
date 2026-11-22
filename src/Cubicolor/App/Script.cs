using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cubicolor.Game;
using Cubicolor.Platform;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;
using Cubicolor.Utilities;

namespace Cubicolor.App;

// Achievements in a scripted run: which were unlocked, and never Steam's own.
internal sealed class ScriptedAchievements : IAchievements
{
    public HashSet<Difficulty> Unlocked { get; } = [];

    public void Unlock(Difficulty finished) => Unlocked.Add(finished);
}

// A scripted run's commands, one per line or separated by ';', `#` starting a comment:
//
//   wait <seconds>              let game time pass
//   wait ready                  until the level takes input (or fail after 10 s), logging when
//   wait completed              until the Completed panel is up
//   wait screen menu|level      until that screen shows
//   wait level <level>          until that level shows
//   wait settled                until the board has come to rest: a roll, a key's paint, a lock's drain
//   press <Key>                 a key for one frame (Key enum names: Up, Return, Escape, R, ...)
//   pad <button>                a controller button for one frame (South, East, Menu, Up, ...)
//   controller on|off           a controller connected (or not) without pressing anything
//   click "<label>"             the widget with that label, as the mouse would
//   moves <FBLR...>             rolls, each once the cube is ready
//   padmoves <FBLR...>          the same with a controller
//   play <level>                straight into a level, e.g. play genius07
//   film <level>                the same for the trailer: no Completed panel, no bell
//   screenshot <path>           the next frame as a PNG (headless)
//   picture                     fail unless the next frame shows something (headless)
//   fail <text>                 fail: reached what should not be
//   mark <text>                 log how the run is doing: memory, and whether it was heard (headless)
//   expect <what> <value>       fail unless it holds: screen (menu|level), level (normal03), state
//                               (ready|busy|paused|completed), solved.<difficulty> (a number), focus,
//                               saved.<difficulty> (the same, on disk), achievement.<difficulty>, moves,
//                               language, dark, names, music, musicmuted, key.<action>
//   expect present|absent <label>   a widget that can be used, or none
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
    private bool _movesByPad, _pendingPicture;
    private float _heard;
    private long _heapAtFirstMark;

    public string? Error { get; private set; }

    // Whether the run's mix is passed to Heard, so silence means something.
    public bool HearsMix { get; init; }

    // A screen the script switches to before the next frame.
    public IScreen? Next { get; set; }
    public bool Done => _lines.Count == 0 && _pendingScreenshot == null && !_pendingPicture && _moves == null && Error == null && _time >= _waitUntil;

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
            Direction direction = DirectionOf(_moves.Dequeue());
            if (_movesByPad) _input.Press((GamepadButtons) (1 << (int) context.Saves.Settings.Pad[ActionFor(direction)]));
            else _input.Press(context.Saves.Settings.Keys[ActionFor(direction)]);
            if (_moves.Count == 0) _moves = null;
            // Give the press a frame to land before checking readiness again.
            _waitUntil = time + 1.0 / 30;
            return;
        }

        while (_lines.Count > 0 && Error == null && time >= _waitUntil && _moves == null && _pendingScreenshot == null && !_pendingPicture)
        {
            string line = _lines.Peek();
            if (!Step(line, time, screen, context)) return;   // still waiting on this line
            if (_lines.Count > 0) _lines.Dequeue();   // a failure has emptied the queue already
        }
    }

    // What the run played since the last frame, mixed: a mark fails on silence, and any bad sample does.
    public void Heard(ReadOnlySpan<float> mix)
    {
        foreach (float sample in mix)
        {
            if (!float.IsFinite(sample)) { Fail("a sample that isn't a number"); return; }
            _heard = Math.Max(_heard, Math.Abs(sample));
        }
    }

    // After a frame has been drawn: look at it, or take the screenshot asked for. In a window, whose frames can't
    // be read back, `renderer` is null and neither happens.
    public void AfterFrame(Render.IRenderer? renderer)
    {
        if (renderer == null)
        {
            _pendingScreenshot = null;
            _pendingPicture = false;
            return;
        }
        if (_pendingPicture)
        {
            _pendingPicture = false;
            if (Colours(renderer.ReadPixels()) < 3) Fail("the frame shows next to nothing");
        }
        if (_pendingScreenshot is not { } path) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Png.Write(path, renderer.ReadPixels(), renderer.Width, renderer.Height, bgra: false, opaque: true);
        Log.Info($"Screenshot: {path}");
        _pendingScreenshot = null;
    }

    // How many colours (16 levels a channel) cover at least a two-thousandth of the frame each.
    private static int Colours(byte[] rgba)
    {
        var counts = new Dictionary<int, int>();
        int samples = 0;
        for (int i = 0; i + 3 < rgba.Length; i += 4 * 7, samples++)
        {
            int colour = (rgba[i] >> 4) << 8 | (rgba[i + 1] >> 4) << 4 | rgba[i + 2] >> 4;
            counts[colour] = counts.GetValueOrDefault(colour) + 1;
        }
        return counts.Values.Count(c => c >= samples / 2000);
    }

    // True once `there` holds; fails the line when it doesn't within ten seconds.
    private bool Until(bool there, string line, double time, IScreen screen)
    {
        if (double.IsPositiveInfinity(_deadline)) _deadline = time + 10;
        if (there || time > _deadline) _deadline = double.PositiveInfinity;
        else return false;
        if (!there) Fail($"{line} (after 10 s: {(screen is LevelScreen l ? $"{LevelName(l)}, {l.State}" : "the menu")})");
        return there;
    }

    private static string LevelName(LevelScreen screen) => LevelFile.FileName(screen.Level.Difficulty, screen.Level.Number)[..^4];

    private bool Step(string line, double time, IScreen screen, GameContext context)
    {
        string[] words = line.Split(' ', 2, StringSplitOptions.TrimEntries);
        string argument = words.Length > 1 ? words[1] : "";
        switch (words[0])
        {
            case "wait" when argument == "ready":
                if (!Until(screen is LevelScreen { State: "ready" }, line, time, screen)) return false;
                Log.Info(FormattableString.Invariant($"Ready at {time:0.000} s"));
                return true;
            case "wait" when argument == "settled":
                return Until(screen is LevelScreen { Board.Settled: true }, line, time, screen);
            case "wait" when argument == "completed":
                return Until(screen is LevelScreen { State: "completed" }, line, time, screen);
            case "wait" when argument is "screen menu" or "screen level":
                return Until(screen is LevelScreen == (argument == "screen level"), line, time, screen);
            case "wait" when argument.StartsWith("level ", StringComparison.Ordinal):
                return Until(screen is LevelScreen shown && LevelName(shown) == argument[6..], line, time, screen);
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
            case "controller":
                _input.Connected = argument == "on";
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
            case "moves" or "padmoves":
                _moves = new Queue<char>(argument);
                _movesByPad = words[0] == "padmoves";
                _deadline = time + 10 + argument.Length;
                return true;
            case "play" or "film":
                if (argument.Length < 3 || !Enum.TryParse(argument[..^2], true, out Difficulty difficulty)
                    || !int.TryParse(argument[^2..], out int number)) { Fail(line); return true; }
                Next = new LevelScreen(context, difficulty, number) { Filmed = words[0] == "film" };
                // The next line waits for the new screen to be showing.
                _waitUntil = time + 1e-6;
                return true;
            case "screenshot":
                _pendingScreenshot = argument;
                return true;
            case "fail":
                Fail(argument);
                return true;
            case "picture":
                _pendingPicture = true;
                return true;
            case "mark":
                long heap = GC.GetTotalMemory(forceFullCollection: true);
                if (_heapAtFirstMark == 0) _heapAtFirstMark = heap;
                Log.Info(FormattableString.Invariant(
                    $"Mark: {argument} at {time:0} s; heap {heap / 1048576.0:0.0} MB, process {Environment.WorkingSet / 1048576} MB, loudest {_heard:0.000}"));
                if (heap > _heapAtFirstMark + (16 << 20)) Fail($"{line}: the heap has grown by {(heap - _heapAtFirstMark) / 1048576} MB since the first mark");
                if (HearsMix && _heard < 0.01f) Fail($"{line}: nothing was heard");
                _heard = 0;
                return true;
            case "expect" when argument.StartsWith("present ", StringComparison.Ordinal) || argument.StartsWith("absent ", StringComparison.Ordinal):
                string[] widgetExpected = argument.Split(' ', 2);
                bool present = context.Ui.Widgets.Any(w => w.Label == widgetExpected[1].Trim('"'));
                if (present != (widgetExpected[0] == "present"))
                    Fail($"{line}: on screen are {string.Join(", ", context.Ui.Widgets.Select(w => $"\"{w.Label}\""))}");
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
        "level" => screen is LevelScreen l ? LevelName(l) : "none",
        "moves" => screen is LevelScreen l ? l.Puzzle.Moves.ToString(CultureInfo.InvariantCulture) : "none",
        "language" => context.Language.Code(),
        "dark" => context.Saves.Settings.DarkBackground ? "true" : "false",
        "names" => context.Saves.Settings.HideUi ? "false" : "true",
        "music" => context.Saves.Settings.MusicVolume.ToString("0.##", CultureInfo.InvariantCulture),
        "musicmuted" => context.Saves.Settings.MusicMuted ? "true" : "false",
        _ when what.StartsWith("key.", StringComparison.Ordinal) && Enum.TryParse(what[4..], true, out GameAction action)
            => context.Saves.Settings.Keys[action].ToString(),
        _ when what.StartsWith("saved.", StringComparison.Ordinal) && Enum.TryParse(what[6..], true, out Difficulty saved)
            => context.Saves.SavedProgress().Solved(saved).ToString(CultureInfo.InvariantCulture),
        _ when what.StartsWith("achievement.", StringComparison.Ordinal) && Enum.TryParse(what[12..], true, out Difficulty unlocked)
            => context.Achievements is ScriptedAchievements a && a.Unlocked.Contains(unlocked) ? "true" : "false",
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

    private static GameAction ActionFor(Direction direction) => direction switch
    {
        Direction.Forwards => GameAction.Forwards,
        Direction.Backwards => GameAction.Backwards,
        Direction.Left => GameAction.Left,
        _ => GameAction.Right,
    };

    private void Fail(string message)
    {
        Error ??= message;
        _lines.Clear();
        _moves = null;
    }
}
