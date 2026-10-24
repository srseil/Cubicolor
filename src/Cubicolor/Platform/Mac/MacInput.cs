using System;
using System.Collections.Generic;
using System.Numerics;
using Cubicolor.Simulation.Input;
using static Cubicolor.Platform.Mac.ObjC;

namespace Cubicolor.Platform.Mac;

// The live input on macOS: each Poll() runs the AppKit event pump, sees every keyboard, mouse and scroll
// event on the way through, and snapshots keys and buttons so edge queries (just pressed) work. Everything
// reads up again when the window loses focus, so a key released while another app had it does not stick.
internal sealed class MacInput : IInput
{
    private readonly MacWindow _window;

    private readonly bool[] _held = new bool[256];
    private bool[] _current = new bool[256];
    private bool[] _previous = new bool[256];
    // Keys with a (non-repeat) press event this frame, so a key that goes down and up within one frame is
    // still seen as pressed.
    private readonly HashSet<int> _eventPressed = [];

    private Vector2 _mousePosition;
    private int _buttonsHeld, _buttons, _buttonsPrevious;
    private float _scrollDelta;
    private bool _focused;
    private GamepadState _gamepad;

    public MacInput(MacWindow window)
    {
        _window = window;
        _focused = window.IsFocused;
    }

    public Vector2 MousePosition => _mousePosition;
    public float ScrollDelta => _scrollDelta;
    public GamepadState Gamepad => _gamepad;

    public void Poll()
    {
        _scrollDelta = 0f;
        _eventPressed.Clear();

        AppKit.PumpEvents(Observe);
        if (AppKit.QuitRequested) _window.RequestClose();
        _window.RefreshSize();

        bool focused = _window.IsFocused;
        if (focused != _focused)
        {
            _focused = focused;
            if (!focused)
            {
                Array.Clear(_held);
                _buttonsHeld = 0;
            }
        }

        (_previous, _current) = (_current, _previous);
        Array.Copy(_held, _current, _held.Length);
        _buttonsPrevious = _buttons;
        _buttons = _buttonsHeld;
        (float x, float y) = _window.MouseLocation();
        _mousePosition = new Vector2(x, y) * _window.PixelDensity;
        _gamepad = GameControllers.Read();
    }

    private static readonly IntPtr SelType = Sel("type"), SelKeyCode = Sel("keyCode"), SelRepeat = Sel("isARepeat"),
        SelFlags = Sel("modifierFlags"), SelButton = Sel("buttonNumber"), SelDeltaY = Sel("deltaY"),
        SelScrollY = Sel("scrollingDeltaY"), SelPrecise = Sel("hasPreciseScrollingDeltas"), SelInverted = Sel("isDirectionInvertedFromDevice");

    private void Observe(IntPtr e)
    {
        switch (Send<nuint>(e, SelType))
        {
            case 10:   // key down
            {
                Key key = KeyCodes.ToKey(Send<ushort>(e, SelKeyCode));
                if (key == Key.Unknown) break;
                if (Send<byte>(e, SelRepeat) == 0) _eventPressed.Add((int) key);
                _held[(int) key] = true;
                break;
            }
            case 11:   // key up
                _held[(int) KeyCodes.ToKey(Send<ushort>(e, SelKeyCode))] = false;
                break;
            case 12:   // a modifier went down or up: which, from its key code, and which way, from its own flag
            {
                Key key = KeyCodes.ToKey(Send<ushort>(e, SelKeyCode));
                ulong mask = ModifierMask(key);
                if (mask == 0) break;
                bool down = (Send<ulong>(e, SelFlags) & mask) != 0;
                if (down && !_held[(int) key]) _eventPressed.Add((int) key);
                _held[(int) key] = down;
                break;
            }
            case 1: _buttonsHeld |= 1; break;    // left down
            case 2: _buttonsHeld &= ~1; break;   // left up
            case 3: _buttonsHeld |= 4; break;    // right down
            case 4: _buttonsHeld &= ~4; break;   // right up
            case 25:
                if (Send<nint>(e, SelButton) == 2) _buttonsHeld |= 2;
                break;
            case 26:
                if (Send<nint>(e, SelButton) == 2) _buttonsHeld &= ~2;
                break;
            case 22:   // scroll: precise deltas (trackpads) in points, a wheel's in lines
            {
                double dy = Send<byte>(e, SelPrecise) != 0
                    ? Send<double>(e, SelScrollY) * 0.1
                    : Send<double>(e, SelDeltaY) is var d && d > 0 ? Math.Ceiling(d) : Math.Floor(d);
                if (Send<byte>(e, SelInverted) != 0) dy = -dy;   // natural scrolling: back to + = up
                _scrollDelta += (float) dy;
                break;
            }
        }
    }

    // The device-dependent flag of each modifier key (IOKit's NX_DEVICE*KEYMASK), which tells left from right.
    private static ulong ModifierMask(Key key) => key switch
    {
        Key.LCtrl => 0x1, Key.LShift => 0x2, Key.RShift => 0x4, Key.LGUI => 0x8, Key.RGUI => 0x10,
        Key.LAlt => 0x20, Key.RAlt => 0x40, Key.RCtrl => 0x2000, Key.Capslock => 1ul << 16,
        _ => 0,
    };

    public bool Down(Key key) => _current[(int) key];

    public bool Pressed(Key key) => _eventPressed.Contains((int) key) || (_current[(int) key] && !_previous[(int) key]);

    public IEnumerable<Key> PressedThisFrame()
    {
        foreach (int key in _eventPressed) yield return (Key) key;
    }

    public bool MouseDown(MouseButton button) => (_buttons & Flag(button)) != 0;
    public bool MousePressed(MouseButton button) => (_buttons & Flag(button)) != 0 && (_buttonsPrevious & Flag(button)) == 0;
    public bool MouseReleased(MouseButton button) => (_buttons & Flag(button)) == 0 && (_buttonsPrevious & Flag(button)) != 0;

    private static int Flag(MouseButton button) => button switch
    {
        MouseButton.Left => 1,
        MouseButton.Middle => 2,
        _ => 4,
    };
}
