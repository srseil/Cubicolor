using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Cubicolor.Game;
using Cubicolor.Platform;
using Cubicolor.Render;
using Cubicolor.Simulation.Input;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.App;

// One frame of the game, the same live and headless: input to navigation, the current screen, the draw.
internal sealed class Frame(GameContext context, IScreen first)
{
    private readonly Scene _scene = new();

    public IScreen Screen { get; private set; } = first;

    // How long the last frame's input, game logic and UI took on the CPU, before drawing.
    public double UpdateMs { get; private set; }

    // Whether the last Run left the picture as it was, so nothing was drawn. Only when SkipUnchanged is set:
    // a window that is idle then costs no GPU work at all.
    public bool Unchanged { get; private set; }
    public bool SkipUnchanged { get; init; }

    private readonly Snapshot _drawn = new();

    // `draw` false runs the game without showing it (a hidden window); the next frame then draws in any case.
    public bool Run(IInput input, double seconds, int width, int height, bool draw = true)
    {
        input.Poll();
        context.Audio.Watch();
        context.Ui.Dark = context.Saves.Settings.DarkBackground;
        context.Ui.Begin(input, Navigate(input), width, height, seconds);
        _scene.Items.Clear();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        Screen = Screen.Frame(seconds, _scene);
        UpdateMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        ReadOnlySpan<OverlayVertex> underlay = context.Ui.Underlay.Vertices, overlay = context.Ui.Overlay.Vertices;
        Unchanged = false;
        if (!draw)
        {
            _drawn.Clear();
            return true;
        }
        if (SkipUnchanged && _drawn.Matches(_scene, underlay, overlay, width, height))
        {
            Unchanged = true;
            return true;
        }
        bool drawn = context.Renderer.Draw(_scene, underlay, overlay);
        if (drawn) _drawn.Take(_scene, underlay, overlay, width, height);
        else _drawn.Clear();
        return drawn;
    }

    // A copy of everything a drawn frame was made from, to tell whether the next one would look the same.
    private sealed class Snapshot
    {
        private readonly List<DrawItem> _items = [];
        private readonly List<OverlayVertex> _underlay = [], _overlay = [];
        private (Vector4, Matrix4x4, Matrix4x4, Lighting, int, int)? _frame;

        public bool Matches(Scene scene, ReadOnlySpan<OverlayVertex> underlay, ReadOnlySpan<OverlayVertex> overlay, int width, int height) =>
            _frame == (scene.Background, scene.View, scene.Projection, scene.Lighting, width, height)
            && Same(CollectionsMarshal.AsSpan(scene.Items), CollectionsMarshal.AsSpan(_items))
            && Same(underlay, CollectionsMarshal.AsSpan(_underlay)) && Same(overlay, CollectionsMarshal.AsSpan(_overlay));

        public void Take(Scene scene, ReadOnlySpan<OverlayVertex> underlay, ReadOnlySpan<OverlayVertex> overlay, int width, int height)
        {
            _frame = (scene.Background, scene.View, scene.Projection, scene.Lighting, width, height);
            _items.Clear();
            _items.AddRange(scene.Items);
            _underlay.Clear();
            _underlay.AddRange(underlay);
            _overlay.Clear();
            _overlay.AddRange(overlay);
        }

        public void Clear() => _frame = null;

        private static bool Same<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b) where T : unmanaged =>
            MemoryMarshal.AsBytes(a).SequenceEqual(MemoryMarshal.AsBytes(b));
    }

    public void Show(IScreen screen) => Screen = screen;

    private static Navigation Navigate(IInput input)
    {
        GamepadState pad = input.Gamepad;
        Direction? move = null;
        if (input.Pressed(Key.Up) || pad.WasPressed(GamepadButtons.Up | GamepadButtons.StickUp)) move = Direction.Forwards;
        else if (input.Pressed(Key.Down) || pad.WasPressed(GamepadButtons.Down | GamepadButtons.StickDown)) move = Direction.Backwards;
        else if (input.Pressed(Key.Left) || pad.WasPressed(GamepadButtons.Left | GamepadButtons.StickLeft)) move = Direction.Left;
        else if (input.Pressed(Key.Right) || pad.WasPressed(GamepadButtons.Right | GamepadButtons.StickRight)) move = Direction.Right;
        bool confirm = input.Pressed(Key.Return) || input.Pressed(Key.KpEnter) || input.Pressed(Key.Space) || pad.WasPressed(GamepadButtons.South);
        bool back = input.Pressed(Key.Escape) || pad.WasPressed(GamepadButtons.East | GamepadButtons.Menu);
        return new Navigation(move, confirm, back);
    }
}
