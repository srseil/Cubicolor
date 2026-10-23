using System;
using System.Collections.Generic;

namespace Cubicolor.Game;

public enum Ease
{
    Linear,
    // Slow at both ends: Godot's transition of -1.4, which both earlier versions' falling and rising used.
    InOut,
}

// A value that moves over presentation time: a chain of moves, each from wherever the value is when it starts
// to a new value. A move scheduled to start while another runs takes over from that moment, so nothing ever
// jumps; one scheduled after the last simply follows it.
public sealed class Animated(float value)
{
    private readonly record struct Move(double Start, double Duration, float From, float To, Ease Ease);

    private float _base = value;
    private readonly List<Move> _moves = [];

    public float Target => _moves.Count > 0 ? _moves[^1].To : _base;

    public double EndsAt => _moves.Count > 0 ? _moves[^1].Start + _moves[^1].Duration : double.NegativeInfinity;

    public bool IsMoving(double time) => time < EndsAt;

    public float At(double time)
    {
        float value = _base;
        foreach (Move move in _moves)
        {
            if (time < move.Start) break;
            value = move.Duration <= 0 || time >= move.Start + move.Duration
                ? move.To
                : move.From + (move.To - move.From) * Apply(move.Ease, (float) ((time - move.Start) / move.Duration));
        }
        return value;
    }

    // Move to `to` from `start` on, taking `duration` seconds. Moves scheduled to start at or after `start`
    // are dropped: this one replaces them.
    public void MoveTo(float to, double start, double duration, Ease ease = Ease.Linear)
    {
        _moves.RemoveAll(m => m.Start >= start);
        _moves.Add(new Move(start, duration, At(start), to, ease));
    }

    public void Set(float value)
    {
        _moves.Clear();
        _base = value;
    }

    // Fold the moves that have ended by `time` into the base value, so the chain stays short.
    public void Settle(double time)
    {
        while (_moves.Count > 0 && _moves[0].Start + _moves[0].Duration <= time)
        {
            _base = _moves[0].To;
            _moves.RemoveAt(0);
        }
    }

    public static float Apply(Ease ease, float t) => ease switch
    {
        Ease.InOut => t < 0.5f ? 0.5f * MathF.Pow(2 * t, 1.4f) : 1 - 0.5f * MathF.Pow(2 * (1 - t), 1.4f),
        _ => t,
    };
}
