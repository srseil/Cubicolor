namespace Cubicolor.Simulation.Levels;

public enum TileKind
{
    // No tile: a hole in the board.
    Nothing,
    Floor,
    // Paints the cube in its colour.
    Key,
    // Takes its colour off the cube and one requirement of that colour off the exit.
    Lock,
    // Opens once every requirement is gone; reaching it solves the level.
    Exit,
}

public readonly record struct Tile(TileKind Kind, Paint Paint)
{
    public static readonly Tile Nothing = new(TileKind.Nothing, Paint.None);
    public static readonly Tile Floor = new(TileKind.Floor, Paint.None);
    public static readonly Tile Exit = new(TileKind.Exit, Paint.None);
}
