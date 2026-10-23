using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cubicolor.Simulation.Levels;
using Xunit;

namespace Cubicolor.Tests;

public sealed class LevelTests
{
    private static readonly string Levels = Repo.Assets("levels");

    public static IEnumerable<TheoryDataRow<Difficulty, int>> AllLevels() =>
        from difficulty in Enum.GetValues<Difficulty>()
        from number in Enumerable.Range(1, Level.PerDifficulty)
        select new TheoryDataRow<Difficulty, int>(difficulty, number);

    [Fact]
    public void Normal3_reads_as_the_screenshot_shows_it()
    {
        Level level = LevelFile.Load(Levels, Difficulty.Normal, 3);

        Assert.Equal((4, 5), (level.Columns, level.Rows));
        Assert.Equal(new Cell(2, 4), level.Start);
        Assert.Equal(new Cell(0, 0), level.Exit);
        Assert.Equal([Paint.Blue, Paint.Red], level.Requirements);
        Assert.Equal(new Tile(TileKind.Key, Paint.Red), level[new Cell(3, 0)]);
        Assert.Equal(new Tile(TileKind.Lock, Paint.Blue), level[new Cell(1, 1)]);
        Assert.Equal(new Tile(TileKind.Lock, Paint.Red), level[new Cell(2, 2)]);
        Assert.Equal(new Tile(TileKind.Key, Paint.Blue), level[new Cell(0, 3)]);
        Assert.Equal(Tile.Nothing, level[new Cell(0, 4)]);
    }

    // Every level replays the solution recorded for it: the rules still let each one be solved.
    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_level_replays_its_recorded_solution(Difficulty difficulty, int number)
    {
        Level level = LevelFile.Load(Levels, difficulty, number);
        Assert.NotEmpty(level.Requirements);
        string moves = RecordedSolutions()[LevelFile.FileName(difficulty, number)];

        var puzzle = new Puzzle(level);
        foreach (char move in moves) Assert.True(puzzle.Roll(DirectionOf(move)).Rolled, $"{level}: {move} in {moves}");

        Assert.True(puzzle.Solved);
    }

    // The search behind the recorded solutions takes a minute or two for the Genius levels, so it runs only
    // when asked: CUBICOLOR_SOLVE=check searches every level again, CUBICOLOR_SOLVE=record also rewrites
    // Fixtures/solutions.txt in the source tree.
    [Fact]
    public void Every_level_can_be_solved_from_scratch()
    {
        string? mode = Environment.GetEnvironmentVariable("CUBICOLOR_SOLVE");
        Assert.SkipUnless(mode is "check" or "record", "Set CUBICOLOR_SOLVE=check or record to run the solver");

        var lines = new List<string>();
        foreach (TheoryDataRow<Difficulty, int> row in AllLevels())
        {
            var (difficulty, number) = row.Data;
            Level level = LevelFile.Load(Levels, difficulty, number);
            List<Direction>? solution = Solver.Solve(level);
            Assert.True(solution != null, $"{level} has no solution");
            lines.Add($"{LevelFile.FileName(difficulty, number)} {string.Concat(solution.Select(LetterOf))}");
        }
        if (mode == "record")
            File.WriteAllLines(Path.Combine(Repo.Root, "tests", "Cubicolor.Tests", "Fixtures", "solutions.txt"), lines);
    }

    private static Dictionary<string, string> RecordedSolutions() =>
        File.ReadAllLines(Repo.Fixture("solutions.txt"))
            .Select(line => line.Split(' '))
            .ToDictionary(parts => parts[0], parts => parts[1]);

    private static char LetterOf(Direction direction) => direction switch
    {
        Direction.Forwards => 'F',
        Direction.Backwards => 'B',
        Direction.Left => 'L',
        _ => 'R',
    };

    private static Direction DirectionOf(char letter) => letter switch
    {
        'F' => Direction.Forwards,
        'B' => Direction.Backwards,
        'L' => Direction.Left,
        'R' => Direction.Right,
        _ => throw new ArgumentException($"Unknown move {letter}"),
    };

    [Fact]
    public void A_file_of_the_wrong_length_is_rejected()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(Levels, "normal01.bin"));
        Assert.Throws<InvalidDataException>(() => LevelFile.Parse(data.AsSpan(0, data.Length - 1), Difficulty.Normal, 1));
    }
}
