using Cubicolor.Simulation.Levels;
using Xunit;

namespace Cubicolor.Tests;

public sealed class PuzzleTests
{
    // A board drawn as text, one character per cell:
    //   . nothing   # floor   E exit   S start (floor)
    //   r g b y     keys      R G B Y  locks
    private static Level Board(string requirementsBottomUp, params string[] rows)
    {
        int columns = rows[0].Length;
        var tiles = new Tile[columns * rows.Length];
        Cell start = default;
        for (int row = 0; row < rows.Length; row++)
        for (int column = 0; column < columns; column++)
        {
            char c = rows[row][column];
            if (c == 'S') start = new Cell(column, row);
            tiles[row * columns + column] = c switch
            {
                '.' => Tile.Nothing,
                '#' or 'S' => Tile.Floor,
                'E' => Tile.Exit,
                _ => new Tile(char.IsLower(c) ? TileKind.Key : TileKind.Lock, PaintOf(c)),
            };
        }
        var requirements = new Paint[requirementsBottomUp.Length];
        for (int i = 0; i < requirements.Length; i++) requirements[i] = PaintOf(requirementsBottomUp[i]);
        return new Level(Difficulty.Normal, 1, columns, rows.Length, tiles, start, requirements);
    }

    private static Paint PaintOf(char c) => char.ToLowerInvariant(c) switch
    {
        'r' => Paint.Red,
        'g' => Paint.Green,
        'b' => Paint.Blue,
        'y' => Paint.Yellow,
        _ => Paint.None,
    };

    [Fact]
    public void The_tile_left_behind_falls_away()
    {
        var puzzle = new Puzzle(Board("r", "S#rRE"));

        Assert.True(puzzle.Roll(Direction.Right).Rolled);

        Assert.False(puzzle.IsStanding(new Cell(0, 0)));
        Assert.Equal(Blocked.NoTile, puzzle.Roll(Direction.Left).Blocked);
        Assert.Equal(new Cell(1, 0), puzzle.Position);
    }

    [Fact]
    public void A_key_paints_the_cube_and_replaces_what_it_carried()
    {
        var puzzle = new Puzzle(Board("b", "SrbB", "...E"));

        Assert.Equal(Paint.Red, puzzle.Roll(Direction.Right).PaintAfter);
        Move move = puzzle.Roll(Direction.Right);

        Assert.Equal((Paint.Red, Paint.Blue), (move.PaintBefore, move.PaintAfter));
    }

    [Fact]
    public void A_lock_of_another_colour_does_nothing()
    {
        var puzzle = new Puzzle(Board("b", "SrB#", "..bE"));
        puzzle.Roll(Direction.Right);

        Move move = puzzle.Roll(Direction.Right);

        Assert.Equal(Paint.Red, move.PaintAfter);
        Assert.Equal(-1, move.Delivered);
    }

    [Fact]
    public void Delivering_takes_the_topmost_requirement_of_that_colour()
    {
        // Bottom up: red, blue, red. The first red delivered is the top one.
        var puzzle = new Puzzle(Board("rbr", "SrRE"));
        puzzle.Roll(Direction.Right);

        Move move = puzzle.Roll(Direction.Right);

        Assert.Equal(2, move.Delivered);
        Assert.Equal(Paint.None, move.PaintAfter);
        Assert.Equal(2, puzzle.RequirementsLeft);
    }

    [Fact]
    public void The_exit_opens_with_the_last_requirement_and_reaching_it_solves_the_level()
    {
        var puzzle = new Puzzle(Board("r", "SrRE"));

        Assert.Equal(Blocked.NoTile, puzzle.CanMove(Direction.Left));
        puzzle.Roll(Direction.Right);
        Move delivered = puzzle.Roll(Direction.Right);
        Assert.True(delivered.ExitOpened);
        Move last = puzzle.Roll(Direction.Right);

        Assert.True(last.Solved);
        Assert.Equal(Blocked.Solved, puzzle.CanMove(Direction.Left));
    }

    [Fact]
    public void A_closed_exit_blocks_differently_from_a_hole()
    {
        var puzzle = new Puzzle(Board("r", "SE", "rR"));

        Assert.Equal(Blocked.ExitClosed, puzzle.CanMove(Direction.Right));
        Assert.Equal(Blocked.NoTile, puzzle.CanMove(Direction.Forwards));
    }

    [Fact]
    public void Restart_puts_everything_back()
    {
        var puzzle = new Puzzle(Board("r", "SrRE"));
        puzzle.Roll(Direction.Right);
        puzzle.Roll(Direction.Right);

        puzzle.Restart();

        Assert.Equal(new Cell(0, 0), puzzle.Position);
        Assert.Equal(Paint.None, puzzle.Carried);
        Assert.True(puzzle.IsStanding(new Cell(1, 0)));
        Assert.False(puzzle.ExitOpen);
        Assert.Equal(1, puzzle.RequirementsLeft);
    }
}
