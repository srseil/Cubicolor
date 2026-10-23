using System;

namespace Cubicolor.Simulation.Levels;

// A cell on the board: Column runs right (+x in the world), Row runs towards the viewer (+z).
public readonly record struct Cell(int Column, int Row)
{
    public Cell Step(Direction direction) => direction switch
    {
        Direction.Forwards => this with { Row = Row - 1 },
        Direction.Backwards => this with { Row = Row + 1 },
        Direction.Left => this with { Column = Column - 1 },
        Direction.Right => this with { Column = Column + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    public override string ToString() => $"({Column}, {Row})";
}

// The four ways the cube can roll. Forwards is away from the viewer.
public enum Direction
{
    Forwards,
    Backwards,
    Left,
    Right,
}
