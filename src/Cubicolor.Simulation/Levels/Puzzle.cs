using System;
using System.Collections.Generic;

namespace Cubicolor.Simulation.Levels;

// Why a roll didn't happen.
public enum Blocked
{
    None,
    // Off the board, onto a hole, or onto a tile the cube has already left (tiles fall away behind it).
    NoTile,
    // The exit, while requirements are still on it.
    ExitClosed,
    // The level is already solved.
    Solved,
}

// What one roll changed, for the presentation to animate. The rules have already moved on when it is
// returned: nothing here waits for an animation.
public readonly record struct Move(
    Direction Direction,
    Cell From,
    Cell To,
    Paint PaintBefore,
    Paint PaintAfter,
    // The requirement taken off the exit's stack (an index into Level.Requirements), or -1.
    int Delivered,
    bool ExitOpened,
    bool Solved,
    Blocked Blocked)
{
    public bool Rolled => Blocked == Blocked.None;
}

// The rules: one level being played. Rolling the cube onto a tile:
//   - the tile it left falls away, so a path never crosses itself;
//   - a key tile paints the cube in its colour, whatever it carried before;
//   - a lock tile of the colour the cube carries takes the colour off the cube, and the topmost
//     requirement of that colour off the exit's stack; a lock of another colour does nothing;
//   - once the stack is empty the exit opens, and rolling onto it solves the level.
// The cube is stuck once every neighbour is gone; then only a restart helps.
public sealed class Puzzle
{
    private readonly bool[] _standing;
    private readonly bool[] _delivered;

    public Level Level { get; }
    public Cell Position { get; private set; }
    public Paint Carried { get; private set; }
    public bool ExitOpen { get; private set; }
    public bool Solved { get; private set; }
    public int Moves { get; private set; }

    public Puzzle(Level level)
    {
        Level = level;
        _standing = new bool[level.CellCount];
        _delivered = new bool[level.Requirements.Count];
        Restart();
    }

    private Puzzle(Puzzle other)
    {
        Level = other.Level;
        _standing = (bool[]) other._standing.Clone();
        _delivered = (bool[]) other._delivered.Clone();
        Position = other.Position;
        Carried = other.Carried;
        ExitOpen = other.ExitOpen;
        Solved = other.Solved;
        Moves = other.Moves;
    }

    public Puzzle Clone() => new(this);

    public void Restart()
    {
        for (int i = 0; i < _standing.Length; i++)
            _standing[i] = Level[Level.CellAt(i)].Kind is not (TileKind.Nothing or TileKind.Exit);
        Array.Clear(_delivered);
        Position = Level.Start;
        Carried = Paint.None;
        Moves = 0;
        Solved = false;
        ExitOpen = false;
        if (Level.Requirements.Count == 0) OpenExit();
    }

    // Whether the tile at a cell is there to roll onto. The exit stands once it is open.
    public bool IsStanding(Cell cell) => Level.Contains(cell) && _standing[Level.Index(cell)];

    public bool IsDelivered(int requirement) => _delivered[requirement];

    public int RequirementsLeft
    {
        get
        {
            int left = 0;
            foreach (bool done in _delivered) if (!done) left++;
            return left;
        }
    }

    public Blocked CanMove(Direction direction)
    {
        if (Solved) return Blocked.Solved;
        Cell to = Position.Step(direction);
        if (IsStanding(to)) return Blocked.None;
        return Level[to].Kind == TileKind.Exit ? Blocked.ExitClosed : Blocked.NoTile;
    }

    public Move Roll(Direction direction)
    {
        Cell from = Position;
        Cell to = from.Step(direction);
        Paint before = Carried;
        Blocked blocked = CanMove(direction);
        if (blocked != Blocked.None) return new Move(direction, from, from, before, before, -1, false, Solved, blocked);

        _standing[Level.Index(from)] = false;
        Position = to;
        Moves++;

        int delivered = -1;
        bool opened = false;
        Tile tile = Level[to];
        switch (tile.Kind)
        {
            case TileKind.Key:
                Carried = tile.Paint;
                break;
            case TileKind.Lock when tile.Paint == Carried:
                Carried = Paint.None;
                delivered = TopmostWaiting(tile.Paint);
                if (delivered >= 0)
                {
                    _delivered[delivered] = true;
                    if (RequirementsLeft == 0)
                    {
                        OpenExit();
                        opened = true;
                    }
                }
                break;
            case TileKind.Exit:
                Solved = true;
                break;
        }
        return new Move(direction, from, to, before, Carried, delivered, opened, Solved, Blocked.None);
    }

    // The neighbours the cube could roll to now.
    public IEnumerable<Direction> Options()
    {
        foreach (Direction direction in Directions.All)
            if (CanMove(direction) == Blocked.None)
                yield return direction;
    }

    private int TopmostWaiting(Paint paint)
    {
        for (int i = _delivered.Length - 1; i >= 0; i--)
            if (!_delivered[i] && Level.Requirements[i] == paint)
                return i;
        return -1;
    }

    private void OpenExit()
    {
        ExitOpen = true;
        _standing[Level.Index(Level.Exit)] = true;
    }
}

public static class Directions
{
    public static readonly Direction[] All = [Direction.Forwards, Direction.Backwards, Direction.Left, Direction.Right];
}
