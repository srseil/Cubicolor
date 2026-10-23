using System;
using System.Collections.Generic;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.Tests;

// Finds a solution by depth-first search over the puzzle's states, remembering the states it has seen. A
// state is the cube's cell, its paint, which tiles still stand and which requirements are delivered:
// everything the rules read. A branch is cut as soon as the tiles still reachable can no longer finish the
// level (the exit, or enough locks and keys of a colour, cut off behind the path).
internal sealed class Solver
{
    private readonly Level _level;
    private readonly HashSet<(UInt128 Standing, int Position, Paint Carried, int Delivered)> _seen = [];
    private readonly List<Direction> _path = [];
    private readonly int _stateLimit;

    private Solver(Level level, int stateLimit)
    {
        if (level.CellCount > 128) throw new ArgumentException("The solver handles boards of up to 128 cells");
        _level = level;
        _stateLimit = stateLimit;
    }

    public static List<Direction>? Solve(Level level, int stateLimit = 20_000_000)
    {
        var solver = new Solver(level, stateLimit);
        return solver.Search(new Puzzle(level)) ? solver._path : null;
    }

    private bool Search(Puzzle puzzle)
    {
        if (!CanStillFinish(puzzle)) return false;
        if (!_seen.Add(Key(puzzle))) return false;
        if (_seen.Count > _stateLimit) throw new InvalidOperationException($"{_level}: more than {_stateLimit} states");

        foreach (Direction direction in Directions.All)
        {
            if (puzzle.CanMove(direction) != Blocked.None) continue;
            Puzzle next = puzzle.Clone();
            Move move = next.Roll(direction);
            _path.Add(direction);
            if (move.Solved || Search(next)) return true;
            _path.RemoveAt(_path.Count - 1);
        }
        return false;
    }

    private bool CanStillFinish(Puzzle puzzle)
    {
        // Flood the standing tiles from the cube, counting the exit as passable.
        Span<bool> reached = stackalloc bool[_level.CellCount];
        Span<int> stack = stackalloc int[_level.CellCount];
        int top = 0;
        int start = _level.Index(puzzle.Position);
        reached[start] = true;
        stack[top++] = start;
        Span<int> keys = stackalloc int[5];
        Span<int> locks = stackalloc int[5];
        bool exit = false;
        while (top > 0)
        {
            Cell cell = _level.CellAt(stack[--top]);
            foreach (Direction direction in Directions.All)
            {
                Cell next = cell.Step(direction);
                if (!_level.Contains(next)) continue;
                int index = _level.Index(next);
                if (reached[index]) continue;
                Tile tile = _level[next];
                if (tile.Kind == TileKind.Exit)
                {
                    exit = true;
                    reached[index] = true;
                    continue;
                }
                if (!puzzle.IsStanding(next)) continue;
                reached[index] = true;
                stack[top++] = index;
                if (tile.Kind == TileKind.Key) keys[(int) tile.Paint]++;
                if (tile.Kind == TileKind.Lock) locks[(int) tile.Paint]++;
            }
        }
        if (!exit) return false;

        Span<int> needed = stackalloc int[5];
        for (int i = 0; i < _level.Requirements.Count; i++)
            if (!puzzle.IsDelivered(i))
                needed[(int) _level.Requirements[i]]++;
        for (int paint = 1; paint < 5; paint++)
        {
            if (needed[paint] == 0) continue;
            if (locks[paint] < needed[paint]) return false;
            int carried = puzzle.Carried == (Paint) paint ? 1 : 0;
            if (keys[paint] + carried < needed[paint]) return false;
        }
        return true;
    }

    private (UInt128, int, Paint, int) Key(Puzzle puzzle)
    {
        UInt128 standing = 0;
        for (int i = 0; i < _level.CellCount; i++)
            if (puzzle.IsStanding(_level.CellAt(i)))
                standing |= UInt128.One << i;
        int delivered = 0;
        for (int i = 0; i < _level.Requirements.Count; i++)
            if (puzzle.IsDelivered(i))
                delivered |= 1 << i;
        return (standing, _level.Index(puzzle.Position), puzzle.Carried, delivered);
    }
}
