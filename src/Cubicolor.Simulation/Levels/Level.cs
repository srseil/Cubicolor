using System;
using System.Collections.Generic;

namespace Cubicolor.Simulation.Levels;

// One puzzle as it is laid out before the first move: the board, where the cube starts, and the stack of
// colours the exit asks for.
public sealed class Level
{
    public const int PerDifficulty = 16;

    private readonly Tile[] _tiles;

    public Difficulty Difficulty { get; }
    public int Number { get; }
    public int Columns { get; }
    public int Rows { get; }
    public Cell Start { get; }
    public Cell Exit { get; }

    // The exit's requirements from the bottom of the stack up. The top one sits right under the exit block.
    public IReadOnlyList<Paint> Requirements { get; }

    public Level(Difficulty difficulty, int number, int columns, int rows, Tile[] tiles, Cell start, IReadOnlyList<Paint> requirements)
    {
        if (tiles.Length != columns * rows)
            throw new ArgumentException($"{columns}x{rows} board with {tiles.Length} tiles", nameof(tiles));
        Difficulty = difficulty;
        Number = number;
        Columns = columns;
        Rows = rows;
        _tiles = tiles;
        Start = start;
        Requirements = requirements;

        int exits = 0;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].Kind != TileKind.Exit) continue;
            Exit = new Cell(i % columns, i / columns);
            exits++;
        }
        if (exits != 1) throw new ArgumentException($"A level needs exactly one exit, this one has {exits}", nameof(tiles));
        if (!Contains(start) || this[start].Kind is TileKind.Nothing or TileKind.Exit)
            throw new ArgumentException($"The start {start} is not on a tile", nameof(start));
    }

    public bool Contains(Cell cell) => cell.Column >= 0 && cell.Column < Columns && cell.Row >= 0 && cell.Row < Rows;

    public Tile this[Cell cell] => Contains(cell) ? _tiles[Index(cell)] : Tile.Nothing;

    public int Index(Cell cell) => cell.Row * Columns + cell.Column;

    public Cell CellAt(int index) => new(index % Columns, index / Columns);

    public int CellCount => _tiles.Length;

    public override string ToString() => $"{Difficulty} {Number}";
}
