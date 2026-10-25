using System.Collections.Generic;
using System.Numerics;
using Cubicolor.Platform;
using Cubicolor.Simulation.Input;

namespace Cubicolor.App;

// Input for headless runs: what the script presses, for one frame at a time.
internal sealed class ScriptedInput : IInput
{
    private readonly HashSet<Key> _pressNext = [];
    private readonly HashSet<Key> _pressed = [];
    private GamepadButtons _padNext, _pad;
    private bool _clickNext, _released;

    public Vector2 MousePosition { get; set; } = new(-1, -1);
    public float ScrollDelta => 0;
    public GamepadState Gamepad => new(_pad, _pad) { Connected = Connected || _pad != GamepadButtons.None };

    // A controller plugged in for the script, pressed or not.
    public bool Connected { get; set; }

    public void Press(Key key) => _pressNext.Add(key);
    public void Press(GamepadButtons button) => _padNext |= button;
    public void Click(Vector2 position)
    {
        MousePosition = position;
        _clickNext = true;
    }

    public void Poll()
    {
        _pressed.Clear();
        _pressed.UnionWith(_pressNext);
        _pressNext.Clear();
        _pad = _padNext;
        _padNext = GamepadButtons.None;
        _released = _clickNext;
        _clickNext = false;
    }

    public bool Down(Key key) => _pressed.Contains(key);
    public bool Pressed(Key key) => _pressed.Contains(key);
    public IEnumerable<Key> PressedThisFrame() => _pressed;
    public bool MouseDown(MouseButton button) => false;
    public bool MousePressed(MouseButton button) => false;
    // A scripted click is a release over the widget, which is what activates it.
    public bool MouseReleased(MouseButton button) => button == MouseButton.Left && _released;
}
