using System;
using System.Linq;
using System.Numerics;
using Cubicolor.Render;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.Game;

// The meshes the board is drawn with, uploaded once.
public sealed record BoardMeshes(MeshHandle Cube, MeshHandle Slab)
{
    public static BoardMeshes Upload(IRenderer renderer) => new(
        renderer.Upload(RoundedBox.Build(Vector3.One, 0.1f, 10, softness: 2f)),
        renderer.Upload(RoundedBox.Build(new Vector3(1f, 0.5f, 1f), 0.045f, 8, softness: 1.8f)));
}

// What the player sees of a level: every tile, the cube, the exit and its stack of requirements, each with
// the animations the earlier versions gave them (timings from the Godot scenes). The rules (Puzzle) decide;
// this only shows. Start, Show and Restart turn rule events into animations scheduled on the view's own
// clock. Nothing waits for an animation to end except the input, which asks Ready.
//
// World space: cell (column, row) is centred at x = column, z = row, and the tiles' tops are at y = 0. Slabs
// (tiles, requirements, the exit) are half a unit high. A stack level n puts a slab's bottom at y = n / 2:
// requirement i rests at level i, the exit above the last one, and level -1 is down in the board, where the
// open exit can be rolled onto.
public sealed class BoardView
{
    // Durations, in seconds.
    public const double Roll = 0.33;
    public const double Nudge = 0.4;
    public const double TileFall = 0.77;
    public const double CubeFall = 0.67;
    public const double PaintTime = 0.87;
    public const double Fade = 0.3;
    public const double StackStep = 0.56;
    private const float TileDrop = 0.75f;
    private const float CubeDrop = 2.0f;

    private readonly Level _level;
    private readonly BoardMeshes _meshes;
    // Each tile, how far it has dropped, how much of it shows, and how much of its key's plus is left (a key's
    // colour goes into the cube that rolls onto it).
    private readonly (Tile? Tile, Animated Drop, Animated Alpha, Animated Plus)[] _tiles;
    private readonly Animated[] _requirementLevel;
    private readonly Animated[] _requirementAlpha;
    private readonly Animated _exitLevel = new(-1), _exitTurn = new(0), _exitAlpha = new(0);

    private Cell _cube;
    private readonly Animated _cubeHeight = new(CubeDrop), _cubeAlpha = new(0), _paintProgress = new(0);
    private (Direction Direction, double Start, double Duration, float Angle) _roll;
    private Paint _paint;
    private bool _draining;
    private (Paint Paint, double At)? _nextPaint;
    // Where the cube goes back to once it has lifted off, and when (a restart).
    private (Cell Cell, double At)? _home;

    public double Now { get; private set; }

    // Until then the board is still building, or the cube still landing: no input.
    public double ReadyAt { get; private set; }

    public BoardView(Level level, BoardMeshes meshes)
    {
        _level = level;
        _meshes = meshes;
        _tiles = new (Tile?, Animated, Animated, Animated)[level.CellCount];
        for (int i = 0; i < _tiles.Length; i++)
        {
            Tile tile = level[level.CellAt(i)];
            _tiles[i] = (tile.Kind is TileKind.Nothing or TileKind.Exit ? null : tile, new Animated(TileDrop), new Animated(0), new Animated(1));
        }
        _requirementLevel = level.Requirements.Select((_, i) => new Animated(i)).ToArray();
        _requirementAlpha = level.Requirements.Select(_ => new Animated(0)).ToArray();
        _cube = level.Start;
    }

    public void Advance(double seconds)
    {
        Now += seconds;
        foreach (var (_, drop, alpha, plus) in _tiles)
        {
            drop.Settle(Now);
            alpha.Settle(Now);
            plus.Settle(Now);
        }
        if (_home is { } home && Now >= home.At)
        {
            _cube = home.Cell;
            _roll = default;
            _paint = Paint.None;
            _draining = false;
            _home = null;
        }
        foreach (Animated animated in _requirementLevel.Concat(_requirementAlpha)) animated.Settle(Now);
        foreach (Animated animated in new[] { _exitLevel, _exitTurn, _exitAlpha, _cubeHeight, _cubeAlpha, _paintProgress }) animated.Settle(Now);
    }

    // Whether the cube can take the next roll: the last one has (nearly) ended, so a key pressed a moment
    // early is not lost, and rolling onto the exit waits for it to have landed.
    public bool Ready(Cell? target = null)
    {
        if (Now < ReadyAt) return false;
        if (Now < _roll.Start + _roll.Duration * 0.85) return false;
        if (target == _level.Exit && _exitLevel.IsMoving(Now)) return false;
        return true;
    }

    // Whether everything has come to rest.
    public bool Settled => Now >= ReadyAt && Now >= _roll.Start + _roll.Duration && !_exitLevel.IsMoving(Now) && !_paintProgress.IsMoving(Now);

    // The board rises tile by tile in a wave from the back left, the exit climbs to the top of its stack as the
    // requirements appear under it, then the cube drops onto the start.
    public void Start(Puzzle puzzle)
    {
        double wave = Wave(everything: true);
        double stacked = RaiseStack(Now + Fade);
        double land = Math.Max(Now + wave + TileFall, stacked);
        DropCube(land);
        ReadyAt = land + CubeFall;
    }

    // Everything back as it was: the cube lifts off and fades, fallen tiles rise again in the wave, delivered
    // requirements return as the exit climbs back up, then the cube drops onto the start again.
    public void Restart()
    {
        _cubeHeight.MoveTo(CubeDrop, Now, CubeFall, Ease.InOut);
        _cubeAlpha.MoveTo(0, Now, CubeFall);
        _nextPaint = null;
        foreach (var (tile, _, _, plus) in _tiles)
            if (tile?.Kind == TileKind.Key && plus.Target < 1) plus.MoveTo(1, Now, Fade);
        double wave = Wave(everything: false);
        double stacked = RaiseStack(Now);
        double land = Math.Max(Math.Max(Now + CubeFall, Now + wave + TileFall), stacked);
        DropCube(land);
        ReadyAt = land + CubeFall;
    }

    // One roll's consequences.
    public void Show(Move move)
    {
        if (!move.Rolled)
        {
            // The cube tips towards the missing tile and back.
            if (move.Blocked != Blocked.Solved) _roll = (move.Direction, Now, Nudge, 15f);
            return;
        }

        _roll = (move.Direction, Now, Roll, 90f);
        var left = _tiles[_level.Index(move.From)];
        left.Drop.MoveTo(TileDrop, Now + Roll * 0.5, TileFall, Ease.InOut);
        left.Alpha.MoveTo(0, Now + Roll * 0.5, TileFall);
        _cube = move.To;

        double after = Now + Roll;
        var landed = _tiles[_level.Index(move.To)];
        if (landed.Tile?.Kind == TileKind.Key) landed.Plus.MoveTo(0, after, PaintTime);
        if (move.PaintAfter != move.PaintBefore)
        {
            _nextPaint = null;
            if (move.PaintBefore == Paint.None)
            {
                _paint = move.PaintAfter;
                _draining = false;
                _paintProgress.Set(0);
                _paintProgress.MoveTo(1, after, PaintTime);
            }
            else
            {
                // The colour drains away out of the bottom, then, on a key of another colour, the new one rises.
                _draining = true;
                _paintProgress.MoveTo(0, after, PaintTime);
                if (move.PaintAfter != Paint.None) _nextPaint = (move.PaintAfter, after + PaintTime);
            }
        }

        if (move.Delivered >= 0)
        {
            _requirementAlpha[move.Delivered].MoveTo(0, after, Fade);
            double fall = after + Fade;
            for (int i = move.Delivered + 1; i < _requirementLevel.Length; i++)
                _requirementLevel[i].MoveTo(_requirementLevel[i].Target - 1, Math.Max(fall, _requirementLevel[i].EndsAt), StackStep);
            double exitStart = Math.Max(fall, _exitLevel.EndsAt);
            _exitLevel.MoveTo(_exitLevel.Target - 1, exitStart, StackStep);
            _exitTurn.MoveTo(_exitTurn.Target - 90, exitStart, StackStep);
            if (move.ExitOpened)
                // The last step takes it from where the stack stood down into the board, where it can be reached.
                _exitLevel.MoveTo(-1, exitStart + StackStep, StackStep, Ease.InOut);
        }
    }

    // Start the tiles that are down rising, in a wave from the back left (0.1 s per column, 0.2 s per row, as
    // the earlier versions had it). Returns the delay of the last one.
    private double Wave(bool everything)
    {
        double longest = 0;
        for (int i = 0; i < _tiles.Length; i++)
        {
            var (tile, drop, alpha, plus) = _tiles[i];
            if (tile == null || (!everything && drop.Target == 0)) continue;
            plus.Set(1);
            Cell cell = _level.CellAt(i);
            double delay = 0.1 * cell.Column + 0.2 * cell.Row;
            drop.MoveTo(0, Now + delay, TileFall, Ease.InOut);
            alpha.MoveTo(1, Now + delay, TileFall);
            longest = Math.Max(longest, delay);
        }
        return longest;
    }

    // The exit climbs from wherever it is to the top of the stack, a step and a quarter turn at a time. The
    // requirements still showing slide back to their places; each one that is gone fades in again once the
    // exit has climbed past it. Returns when the exit is at the top.
    private double RaiseStack(double start)
    {
        int count = _requirementLevel.Length;
        _exitAlpha.MoveTo(1, start, Fade);
        double t = Math.Max(start, _exitLevel.EndsAt);
        int level = (int) MathF.Round(_exitLevel.At(t));
        for (int i = 0; i < count; i++)
        {
            bool showing = _requirementAlpha[i].Target > 0;
            if (showing) _requirementLevel[i].MoveTo(i, t, StackStep);
            else _requirementLevel[i].Set(i);
        }
        double[] passed = new double[count];
        for (int step = level + 1; step <= count; step++)
        {
            _exitLevel.MoveTo(step, t, StackStep, step == 0 ? Ease.InOut : Ease.Linear);
            _exitTurn.MoveTo(_exitTurn.Target + 90, t, StackStep);
            t += StackStep;
            if (step >= 1) passed[step - 1] = t;
        }
        for (int i = 0; i < count; i++)
            if (_requirementAlpha[i].Target <= 0)
                _requirementAlpha[i].MoveTo(1, Math.Max(passed[i], start), Fade);
        return t;
    }

    // The cube, already lifted off and invisible by `at`, goes back to the start then and drops onto it.
    private void DropCube(double at)
    {
        _home = (_level.Start, at);
        _cubeHeight.MoveTo(0, at, CubeFall, Ease.InOut);
        _cubeAlpha.MoveTo(1, at, CubeFall);
    }

    // The whole board's extent, for the camera: every cell, the stack at its full height, the cube at rest.
    public (Vector3 Min, Vector3 Max) Bounds() =>
        (new Vector3(-0.5f, -0.5f, -0.5f),
         new Vector3(_level.Columns - 0.5f, MathF.Max(1f, 0.5f * (_level.Requirements.Count + 1)), _level.Rows - 0.5f));

    public void Draw(Scene scene)
    {
        if (_nextPaint is { } next && Now >= next.At)
        {
            _paint = next.Paint;
            _draining = false;
            _paintProgress.MoveTo(1, next.At, PaintTime);
            _nextPaint = null;
        }

        for (int i = 0; i < _tiles.Length; i++)
        {
            var (tile, drop, fade, plus) = _tiles[i];
            if (tile is not { } t) continue;
            float alpha = fade.At(Now);
            if (alpha <= 0.001f) continue;
            Cell cell = _level.CellAt(i);
            Matrix4x4 model = Matrix4x4.CreateTranslation(cell.Column, -0.25f - drop.At(Now), cell.Row);
            scene.Items.Add(t.Kind switch
            {
                TileKind.Key => new DrawItem(_meshes.Slab, model, Palette.White.WithAlpha(alpha), Surface.KeyPlus, Palette.Of(t.Paint).WithAlpha(plus.At(Now))),
                TileKind.Lock => new DrawItem(_meshes.Slab, model, Palette.Of(t.Paint).WithAlpha(alpha)),
                _ => new DrawItem(_meshes.Slab, model, Palette.White.WithAlpha(alpha)),
            });
        }

        var exit = new Vector3(_level.Exit.Column, 0, _level.Exit.Row);
        for (int i = 0; i < _requirementLevel.Length; i++)
        {
            float alpha = _requirementAlpha[i].At(Now);
            if (alpha <= 0.001f) continue;
            float y = 0.25f + 0.5f * _requirementLevel[i].At(Now);
            scene.Items.Add(new DrawItem(_meshes.Slab, Matrix4x4.CreateTranslation(exit + new Vector3(0, y, 0)),
                Palette.Of(_level.Requirements[i]).WithAlpha(alpha)));
        }

        float exitAlpha = _exitAlpha.At(Now);
        if (exitAlpha > 0.001f)
        {
            float y = 0.25f + 0.5f * _exitLevel.At(Now);
            Matrix4x4 model = Matrix4x4.CreateRotationY(_exitTurn.At(Now) * MathF.PI / 180) * Matrix4x4.CreateTranslation(exit + new Vector3(0, y, 0));
            scene.Items.Add(new DrawItem(_meshes.Slab, model, Palette.Black.WithAlpha(exitAlpha), Surface.ExitSquare, Palette.White));
        }

        float cubeAlpha = _cubeAlpha.At(Now);
        if (cubeAlpha > 0.001f)
            scene.Items.Add(new DrawItem(_meshes.Cube, CubeModel(), Palette.Black.WithAlpha(cubeAlpha), _draining ? Surface.Drain : Surface.Paint,
                Palette.Of(_paint), _paintProgress.At(Now)));
    }

    // The cube at rest on its cell, or tipped about the bottom edge it is rolling over.
    private Matrix4x4 CubeModel()
    {
        var centre = new Vector3(_cube.Column, 0.5f + _cubeHeight.At(Now), _cube.Row);
        var (direction, start, duration, angle) = _roll;
        float t = duration > 0 ? (float) Math.Clamp((Now - start) / duration, 0, 1) : 1;
        if (t >= 1) return Matrix4x4.CreateTranslation(centre);

        Vector3 d = direction switch
        {
            Direction.Forwards => -Vector3.UnitZ,
            Direction.Backwards => Vector3.UnitZ,
            Direction.Left => -Vector3.UnitX,
            _ => Vector3.UnitX,
        };
        // A roll has already moved the cube to its new cell: it turns from the old one, about the edge between.
        bool rolling = angle >= 90f;
        Vector3 from = rolling ? centre - d : centre;
        Vector3 pivot = from + d * 0.5f - Vector3.UnitY * 0.5f;
        float turn = rolling ? angle * t : angle * (t < 0.5f ? 2 * t : 2 * (1 - t));
        return Matrix4x4.CreateTranslation(from - pivot)
               * Matrix4x4.CreateFromAxisAngle(Vector3.Cross(Vector3.UnitY, d), turn * MathF.PI / 180)
               * Matrix4x4.CreateTranslation(pivot);
    }
}
