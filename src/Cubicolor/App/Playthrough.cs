using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;

namespace Cubicolor.App;

// The whole game as one script, played the way a player would: from a first start with nothing solved, through the
// menus, all 48 levels and the Completed panels to Quit, with the recorded solutions. On the way it changes every
// setting, rebinds keys, pauses, restarts, leaves and comes back, rolls into what blocks the cube, and plays some
// levels with a controller; after each level it expects the progress both in the game and in the file on disk.
internal static class Playthrough
{
    public static string Script(string solutionsFile, string levelsDirectory)
    {
        var solutions = File.ReadLines(solutionsFile).Select(l => l.Split(' ')).Where(p => p.Length == 2)
            .ToDictionary(p => Path.GetFileNameWithoutExtension(p[0]), p => p[1]);
        var lines = new List<string>();
        FirstStart(lines);
        Rebind(lines);
        ChangeSettings(lines);
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            if (difficulty == Difficulty.Smart) lines.AddRange(["click \"Settings\"", "wait 0.4", "click \"Dark Background\"", "wait 0.2", "expect dark true"]);
            if (difficulty == Difficulty.Genius)
            {
                lines.AddRange(["click \"Settings\"", "wait 0.4", "click \"Dark Background\"", "wait 0.2", "expect dark false"]);
                for (int i = 0; i < (int) Language.Japanese; i++) lines.AddRange(["click \"Language\"", "wait 0.1"]);
                lines.Add("expect language ja");
            }
            lines.AddRange(["click \"Play\"", "wait 0.4", $"click \"{difficulty}\"", "wait 0.3", $"click \"{difficulty} 1\""]);
            for (int n = 1; n <= Level.PerDifficulty; n++)
                PlayLevel(lines, LevelFile.Load(levelsDirectory, difficulty, n), solutions[Name(difficulty, n)]);
            lines.Add($"expect achievement.{difficulty} true");
            lines.Add($"mark {difficulty} finished");
        }
        lines.AddRange(["expect saved.normal 16", "expect saved.smart 16", "expect saved.genius 16", "click \"Quit\"", "wait 1", "fail Quit left the game running"]);
        return string.Join('\n', lines);
    }

    private static string Name(Difficulty difficulty, int number) => LevelFile.FileName(difficulty, number)[..^4];

    private static void FirstStart(List<string> lines) => lines.AddRange(
    [
        "wait 0.5", "expect screen menu", "expect focus Credits", "picture",
        "click \"Play\"", "wait 0.4", "expect present Normal 1", "expect absent Normal 2", "expect absent Smart", "expect absent Genius", "picture",
        "expect achievement.Normal false", "expect achievement.Smart false", "expect achievement.Genius false",
    ]);

    private static void Rebind(List<string> lines) => lines.AddRange(
    [
        "click \"Controls\"", "wait 0.4", "picture",
        "click \"Restart key\"", "wait 0.2", "press R", "wait 0.2", "expect key.Restart R",
        // Taking another action's key swaps the two.
        "click \"Forwards key\"", "wait 0.2", "press Down", "wait 0.2", "expect key.Forwards Down", "expect key.Backwards Up",
        "click \"Forwards key\"", "wait 0.2", "press Up", "wait 0.2", "expect key.Forwards Up", "expect key.Backwards Down",
    ]);

    private static void ChangeSettings(List<string> lines)
    {
        lines.AddRange(["click \"Settings\"", "wait 0.4"]);
        Language[] all = Enum.GetValues<Language>();
        foreach (Language language in all.Skip(1).Append(all[0]))
            lines.AddRange(["click \"Language\"", "wait 0.2", $"expect language {language.Code()}", "picture"]);
        lines.AddRange(
        [
            "click \"Dark Background\"", "wait 0.2", "expect dark true", "picture", "click \"Dark Background\"", "wait 0.2", "expect dark false",
            "click \"Show Level Name and Tutorial\"", "wait 0.2", "expect names false", "click \"Show Level Name and Tutorial\"", "wait 0.2", "expect names true",
            "press Down", "wait 0.1", "expect focus Music",
            "press Left", "wait 0.1", "expect music 0.9", "press Return", "wait 0.1", "expect musicmuted true",
            "press Return", "wait 0.1", "expect musicmuted false", "press Right", "wait 0.1", "expect music 1",
            "press Escape", "wait 0.2", "expect focus Play",
        ]);
    }

    private static void PlayLevel(List<string> lines, Level level, string solution)
    {
        int n = level.Number;
        string name = Name(level.Difficulty, n), half = solution[..(solution.Length / 2)];
        lines.AddRange([$"wait level {name}", "wait ready", "wait 0.2", "expect moves 0"]);

        // Into whatever blocks the cube at the start: it nudges, and nothing counts.
        if (Directions.All.Where(d => new Puzzle(level).CanMove(d) != Blocked.None).Select(d => (Direction?) d).FirstOrDefault() is { } blocked)
            lines.AddRange([$"press {KeyFor(blocked)}", "wait 0.6", "expect moves 0"]);

        if (n % 5 == 2)
            lines.AddRange(["press Escape", "wait 0.4", "expect state paused", "expect absent Restart", "press Escape", "wait 0.3", "wait ready"]);
        if (n % 4 == 1)
            lines.AddRange([$"moves {half}", "wait settled", $"expect moves {half.Length}", "press R", "wait 0.1", "expect moves 0", "wait ready"]);
        if (n % 6 == 3)
            lines.AddRange([$"moves {half}", "wait settled", "press Escape", "wait 0.4", "click \"Restart\"", "wait 0.1", "expect moves 0", "wait ready"]);
        if (n == 4)
            lines.AddRange([$"moves {half}", "wait settled", "press Escape", "wait 0.4", "click \"Main Menu\"", "wait screen menu", "wait 0.4",
                            $"click \"{level.Difficulty} {n}\"", $"wait level {name}", "wait ready", "expect moves 0"]);

        Solve();
        if (n == 7)
        {
            lines.AddRange(["click \"Play Again\"", "wait 0.1", "wait ready", "expect moves 0"]);
            Solve();
        }
        lines.AddRange([$"expect solved.{level.Difficulty} {n}", $"expect saved.{level.Difficulty} {n}"]);
        if (n < Level.PerDifficulty) lines.AddRange(n % 2 == 0 ? ["expect focus Next Level", "press Return"] : ["click \"Next Level\""]);
        else lines.AddRange(["expect absent Next Level", "click \"Main Menu\"", "wait screen menu", "wait 0.4"]);

        void Solve() => lines.AddRange(
            [$"{(n % 3 == 0 ? "padmoves" : "moves")} {solution}", "picture", "wait completed", "wait 0.3", $"expect moves {solution.Length}"]);
    }

    private static string KeyFor(Direction direction) => direction switch
    {
        Direction.Forwards => "Up",
        Direction.Backwards => "Down",
        Direction.Left => "Left",
        _ => "Right",
    };
}
