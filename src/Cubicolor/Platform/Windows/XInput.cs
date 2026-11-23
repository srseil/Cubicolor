using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Cubicolor.Utilities;

namespace Cubicolor.Platform.Windows;

// Game controllers through XInput: Xbox pads, and anything Steam Input presents as one (PlayStation and
// Switch pads, when the game runs from Steam). Polled once a frame.
//
// One controller is read: the last one used, else the first connected. Asking about an empty slot is slow
// (it looks for the device each time), so empty slots are looked at again only once a second.
internal sealed unsafe partial class XInput
{
    private const int Slots = 4;
    private const uint NotConnected = 1167;   // ERROR_DEVICE_NOT_CONNECTED

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    private readonly bool[] _connected = new bool[Slots];
    private readonly uint[] _packets = new uint[Slots];
    private long _lastScan = long.MinValue;
    private int _current = -1, _announced = -2;
    private GamepadButtons _previous;

    public GamepadState Read()
    {
        long now = Environment.TickCount64;
        bool scan = now - _lastScan >= 1000;
        if (scan) _lastScan = now;

        State chosen = default;
        int first = -1;
        for (int slot = 0; slot < Slots; slot++)
        {
            if (!_connected[slot] && !scan) continue;
            State state;
            _connected[slot] = XInputGetState((uint) slot, &state) != NotConnected;
            if (!_connected[slot])
            {
                if (_current == slot) _current = -1;
                continue;
            }
            if (first < 0) first = slot;
            // A new packet number: something on it changed, so it is the one in use.
            if (state.PacketNumber != _packets[slot]) _current = slot;
            _packets[slot] = state.PacketNumber;
            if (slot == _current) chosen = state;
        }
        if (_current < 0 && first >= 0)
        {
            _current = first;
            State state;
            XInputGetState((uint) first, &state);
            chosen = state;
        }
        if (_current != _announced)
        {
            _announced = _current;
            Log.Info(_current < 0 ? "Game controller: none" : $"Game controller: XInput {_current + 1}");
        }
        if (_current < 0)
        {
            _previous = GamepadButtons.None;
            return GamepadState.None;
        }

        GamepadButtons held = GamepadButtons.None;
        void Button(int mask, GamepadButtons flag)
        {
            if ((chosen.Buttons & mask) != 0) held |= flag;
        }
        Button(0x0001, GamepadButtons.Up);
        Button(0x0002, GamepadButtons.Down);
        Button(0x0004, GamepadButtons.Left);
        Button(0x0008, GamepadButtons.Right);
        Button(0x0010, GamepadButtons.Menu);      // Start
        Button(0x0020, GamepadButtons.Options);   // Back (View)
        Button(0x0100, GamepadButtons.LeftShoulder);
        Button(0x0200, GamepadButtons.RightShoulder);
        Button(0x1000, GamepadButtons.South);
        Button(0x2000, GamepadButtons.East);
        Button(0x4000, GamepadButtons.West);
        Button(0x8000, GamepadButtons.North);
        held |= GamepadState.StickButtons(new Vector2(chosen.LeftX / 32767f, chosen.LeftY / 32767f));

        GamepadButtons pressed = held & ~_previous;
        _previous = held;
        return new GamepadState(held, pressed) { Connected = true };
    }

    // xinput1_4 is part of Windows since 8.
    [LibraryImport("xinput1_4.dll")] private static partial uint XInputGetState(uint slot, State* state);
}
