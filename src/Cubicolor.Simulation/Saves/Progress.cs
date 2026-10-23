using System;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.Simulation.Saves;

// How far the player has come: per difficulty, how many levels are solved, or Locked. Levels are solved in
// order, so a count says it all: level n is open once n - 1 are solved. Solving level 16 of one difficulty
// opens the next. Both earlier versions kept the same information (the Godot one as exactly these counts).
public sealed class Progress
{
    public const int Locked = -1;

    private readonly int[] _solved = [0, Locked, Locked];

    public int Solved(Difficulty difficulty) => _solved[(int) difficulty];

    public bool IsOpen(Difficulty difficulty, int number) =>
        Solved(difficulty) != Locked && number >= 1 && number <= Level.PerDifficulty && number <= Solved(difficulty) + 1;

    public bool IsSolved(Difficulty difficulty, int number) => number <= Solved(difficulty);

    // Records a solved level. Replaying one that was solved before changes nothing.
    public void Complete(Difficulty difficulty, int number)
    {
        if (!IsOpen(difficulty, number)) throw new InvalidOperationException($"{difficulty} {number} is not open");
        Raise(difficulty, number);
    }

    // Takes over whatever another record has that this one doesn't. Progress only grows, so merging the
    // records of every version the player may have played is always safe.
    public void Merge(Progress other)
    {
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
            Raise(difficulty, other.Solved(difficulty));
    }

    // Sets a difficulty to at least `solved`, keeping the unlock rule: a finished difficulty opens the next.
    internal void Raise(Difficulty difficulty, int solved)
    {
        solved = Math.Clamp(solved, Locked, Level.PerDifficulty);
        int i = (int) difficulty;
        if (solved <= _solved[i]) return;
        _solved[i] = solved;
        if (solved == Level.PerDifficulty && difficulty != Difficulty.Genius)
            Raise(difficulty + 1, 0);
    }

    public override string ToString() => $"normal {_solved[0]}, smart {_solved[1]}, genius {_solved[2]}";
}
