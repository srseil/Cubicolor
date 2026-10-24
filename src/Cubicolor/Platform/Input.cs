using System;
using System.Collections.Generic;
using System.Numerics;
using Cubicolor.Simulation.Input;

namespace Cubicolor.Platform;

public enum MouseButton
{
    Left,
    Middle,
    Right,
}

// A game controller's buttons, named for the Xbox layout's positions: South is A on an Xbox pad and Cross on a
// PlayStation one, Menu the right one of the two middle buttons. The left stick's directions count as buttons
// once pushed past half way, so menus and the cube can be steered with it like with the d-pad.
[Flags]
public enum GamepadButtons
{
    None = 0,
    South = 1 << 0, East = 1 << 1, West = 1 << 2, North = 1 << 3,
    LeftShoulder = 1 << 4, RightShoulder = 1 << 5,
    Menu = 1 << 6, Options = 1 << 7,
    Up = 1 << 8, Down = 1 << 9, Left = 1 << 10, Right = 1 << 11,
    StickUp = 1 << 12, StickDown = 1 << 13, StickLeft = 1 << 14, StickRight = 1 << 15,
}

// One frame of a game controller: the buttons held and those newly pressed this frame.
public readonly record struct GamepadState(GamepadButtons Held, GamepadButtons Pressed)
{
    public static readonly GamepadState None = default;

    public bool Connected { get; init; }

    public bool IsHeld(GamepadButtons button) => (Held & button) != 0;
    public bool WasPressed(GamepadButtons button) => (Pressed & button) != 0;

    // The stick's directions as buttons: each held while the stick is pushed past half way along it.
    public static GamepadButtons StickButtons(Vector2 stick)
    {
        const float threshold = 0.5f;
        GamepadButtons held = GamepadButtons.None;
        if (stick.Y > threshold) held |= GamepadButtons.StickUp;
        if (-stick.Y > threshold) held |= GamepadButtons.StickDown;
        if (-stick.X > threshold) held |= GamepadButtons.StickLeft;
        if (stick.X > threshold) held |= GamepadButtons.StickRight;
        return held;
    }
}

// What the game reads from the player each frame. Poll() advances the snapshot once at the top of a frame;
// every query then reports that frame's state. The live implementation pumps the platform's events; the
// scripted one replays a headless run's commands.
public interface IInput
{
    void Poll();

    bool Down(Key key);
    // Went down this frame (initial press only).
    bool Pressed(Key key);
    // Every key that went down this frame, for capturing a new binding.
    IEnumerable<Key> PressedThisFrame();

    // In pixels of the drawn frame from its top left, the space the UI lays out in (window points times the
    // display's pixel density).
    Vector2 MousePosition { get; }
    bool MouseDown(MouseButton button);
    bool MousePressed(MouseButton button);
    bool MouseReleased(MouseButton button);
    // Wheel movement this frame; positive is up / away.
    float ScrollDelta { get; }

    GamepadState Gamepad { get; }
}
