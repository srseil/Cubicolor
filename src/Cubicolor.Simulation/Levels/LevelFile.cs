using System;
using System.Collections.Generic;
using System.IO;

namespace Cubicolor.Simulation.Levels;

// The level format both earlier versions shipped (assets/levels/<difficulty><nn>.bin), kept byte for byte:
//
//   start row, start column                  1 byte each
//   requirements                             8 bytes: 0 none, 1 red, 2 green, 3 blue, 4 yellow;
//                                            listed top of the stack first
//   rows, columns                            1 byte each
//   tiles                                    rows * columns bytes, row by row:
//                                            0 nothing, 1 floor, 2 exit,
//                                            3/4 red key/lock, 5/6 green, 7/8 blue, 9/10 yellow
public static class LevelFile
{
    private const int RequirementSlots = 8;

    public static string FileName(Difficulty difficulty, int number) =>
        $"{difficulty.ToString().ToLowerInvariant()}{number:00}.bin";

    public static Level Load(string directory, Difficulty difficulty, int number) =>
        Parse(File.ReadAllBytes(Path.Combine(directory, FileName(difficulty, number))), difficulty, number);

    public static Level Parse(ReadOnlySpan<byte> data, Difficulty difficulty, int number)
    {
        const int header = 2 + RequirementSlots + 2;
        if (data.Length < header) throw new InvalidDataException($"A level file has at least {header} bytes, this one {data.Length}");

        var start = new Cell(data[1], data[0]);

        // Listed top first; kept bottom first, the way the stack is built and drawn.
        var requirements = new List<Paint>();
        for (int i = RequirementSlots - 1; i >= 0; i--)
        {
            byte id = data[2 + i];
            if (id == 0) continue;
            if (id > 4) throw new InvalidDataException($"Unknown requirement {id}");
            requirements.Add((Paint) id);
        }

        int rows = data[2 + RequirementSlots];
        int columns = data[3 + RequirementSlots];
        if (data.Length != header + rows * columns)
            throw new InvalidDataException($"A {columns}x{rows} level has {header + rows * columns} bytes, this one {data.Length}");

        var tiles = new Tile[rows * columns];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = TileOf(data[header + i]);

        return new Level(difficulty, number, columns, rows, tiles, start, requirements);
    }

    private static Tile TileOf(byte id) => id switch
    {
        0 => Tile.Nothing,
        1 => Tile.Floor,
        2 => Tile.Exit,
        >= 3 and <= 10 => new Tile(id % 2 == 1 ? TileKind.Key : TileKind.Lock, (Paint) ((id - 1) / 2)),
        _ => throw new InvalidDataException($"Unknown tile {id}"),
    };
}
