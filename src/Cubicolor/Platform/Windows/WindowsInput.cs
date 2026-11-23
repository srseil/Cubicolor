using System;
using System.Collections.Generic;
using System.Numerics;
using Cubicolor.Simulation.Input;
using static Cubicolor.Platform.Windows.Win32;

namespace Cubicolor.Platform.Windows;

// The live input on Windows: each Poll() pumps the window's messages, sees every key, mouse button and wheel
// message on the way through, and snapshots keys and buttons so edge queries (just pressed) work. Everything
// reads up again when the window loses focus, so a key released while another app had it does not stick.
internal sealed class WindowsInput : IInput
{
    private readonly Win32Window _window;

    private readonly bool[] _held = new bool[256];
    private bool[] _current = new bool[256];
    private bool[] _previous = new bool[256];
    // Keys with a (non-repeat) press message this frame, so a key that goes down and up within one frame is
    // still seen as pressed.
    private readonly HashSet<int> _eventPressed = [];

    private Vector2 _mousePosition;
    private int _buttonsHeld, _buttons;
    // Buttons that went down or up during this frame's messages: a quick click can do both between two frames.
    private int _buttonsWentDown, _buttonsWentUp;
    private float _scrollDelta;
    private bool _focused;
    private GamepadState _gamepad;
    private readonly XInput _pads = new();

    public WindowsInput(Win32Window window)
    {
        _window = window;
        _focused = window.IsFocused;
        window.Observe = Observe;
    }

    public Vector2 MousePosition => _mousePosition;
    public float ScrollDelta => _scrollDelta;
    public GamepadState Gamepad => _gamepad;

    public void Poll()
    {
        _scrollDelta = 0f;
        _eventPressed.Clear();
        _buttonsWentDown = _buttonsWentUp = 0;

        _window.Pump();

        bool focused = _window.IsFocused;
        if (focused != _focused)
        {
            _focused = focused;
            if (!focused) LetGo();
        }

        (_previous, _current) = (_current, _previous);
        Array.Copy(_held, _current, _held.Length);
        _buttons = _buttonsHeld;
        (float x, float y) = _window.MouseLocation();
        _mousePosition = new Vector2(x, y);
        // A controller moves the game only while it has the focus, as on macOS.
        GamepadState pad = _pads.Read();
        _gamepad = focused ? pad : GamepadState.None with { Connected = pad.Connected };
    }

    private void LetGo()
    {
        Array.Clear(_held);
        _buttonsHeld = 0;
    }

    private void Observe(uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case WM_KEYDOWN or WM_SYSKEYDOWN:
            {
                Key key = KeyOf(wParam, lParam);
                if (key == Key.Unknown) break;
                if ((lParam & (1 << 30)) == 0) _eventPressed.Add((int) key);   // bit 30: the key was down already, a repeat
                _held[(int) key] = true;
                break;
            }
            case WM_KEYUP or WM_SYSKEYUP:
            {
                Key key = KeyOf(wParam, lParam);
                // Print Screen sends only its release.
                if (key == Key.Printscreen) _eventPressed.Add((int) key);
                _held[(int) key] = false;
                // Both Shift keys down, one let go: Windows reports only one release, for either.
                if (key is Key.LShift or Key.RShift) _held[(int) Key.LShift] = _held[(int) Key.RShift] = false;
                break;
            }
            case WM_KILLFOCUS: LetGo(); break;
            case WM_LBUTTONDOWN: ButtonDown(1); break;
            case WM_LBUTTONUP: ButtonUp(1); break;
            case WM_MBUTTONDOWN: ButtonDown(2); break;
            case WM_MBUTTONUP: ButtonUp(2); break;
            case WM_RBUTTONDOWN: ButtonDown(4); break;
            case WM_RBUTTONUP: ButtonUp(4); break;
            case WM_MOUSEWHEEL:
                _scrollDelta += (short) ((wParam >> 16) & 0xFFFF) / 120f;   // in notches, positive away from the user
                break;
        }
    }

    // The scan code is bits 16-23 of lParam, the extended flag bit 24.
    private static Key KeyOf(nuint wParam, nint lParam) => ScanCodes.ToKey((int) ((lParam >> 16) & 0xFF), (lParam & (1 << 24)) != 0, wParam);

    public bool Down(Key key) => _current[(int) key];

    public bool Pressed(Key key) => _eventPressed.Contains((int) key) || (_current[(int) key] && !_previous[(int) key]);

    public IEnumerable<Key> PressedThisFrame()
    {
        foreach (int key in _eventPressed) yield return (Key) key;
    }

    private void ButtonDown(int flag)
    {
        _buttonsHeld |= flag;
        _buttonsWentDown |= flag;
    }

    private void ButtonUp(int flag)
    {
        _buttonsHeld &= ~flag;
        _buttonsWentUp |= flag;
    }

    public bool MouseDown(MouseButton button) => (_buttons & Flag(button)) != 0;
    public bool MousePressed(MouseButton button) => (_buttonsWentDown & Flag(button)) != 0;
    public bool MouseReleased(MouseButton button) => (_buttonsWentUp & Flag(button)) != 0;

    private static int Flag(MouseButton button) => button switch
    {
        MouseButton.Left => 1,
        MouseButton.Middle => 2,
        _ => 4,
    };
}
