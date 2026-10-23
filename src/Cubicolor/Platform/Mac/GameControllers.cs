using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Cubicolor.Utilities;
using static Cubicolor.Platform.Mac.ObjC;

namespace Cubicolor.Platform.Mac;

// Game controllers through Apple's GameController framework: Xbox and PlayStation pads over USB or Bluetooth,
// read once a frame from the extended gamepad profile. Polled, so no callbacks: the framework keeps each
// controller's state current as the run loop runs, which the event pump does every frame. It delivers input
// only while the game is the frontmost application, which is what a game wants.
//
// One controller is read: the one the system calls current (the last one used), else the first connected.
internal static class GameControllers
{
    private static IntPtr _class;
    private static GamepadButtons _previous;
    private static IntPtr _announced;

    private static readonly IntPtr SelControllers = Sel("controllers"), SelCurrent = Sel("current"), SelCount = Sel("count"),
        SelAt = Sel("objectAtIndex:"), SelExtended = Sel("extendedGamepad"), SelPressed = Sel("isPressed"), SelValue = Sel("value"),
        SelX = Sel("xAxis"), SelY = Sel("yAxis"), SelVendor = Sel("vendorName"), SelResponds = Sel("respondsToSelector:");

    public static GamepadState Read()
    {
        if (_class == IntPtr.Zero)
        {
            NativeLibrary.Load("/System/Library/Frameworks/GameController.framework/GameController");
            _class = Class("GCController");
        }

        using var pool = AutoreleasePool.Push();
        IntPtr controller = Send<IntPtr>(_class, SelCurrent);
        if (controller == IntPtr.Zero || Send<IntPtr>(controller, SelExtended) == IntPtr.Zero)
        {
            controller = IntPtr.Zero;
            IntPtr all = Send<IntPtr>(_class, SelControllers);
            for (nuint i = 0, n = Send<nuint>(all, SelCount); i < n && controller == IntPtr.Zero; i++)
            {
                IntPtr candidate = Send<IntPtr, nuint>(all, SelAt, i);
                if (Send<IntPtr>(candidate, SelExtended) != IntPtr.Zero) controller = candidate;
            }
        }
        if (controller != _announced)
        {
            _announced = controller;
            Log.Info(controller == IntPtr.Zero ? "Game controller: none" : $"Game controller: {Str(Send<IntPtr>(controller, SelVendor)) ?? "unnamed"}");
        }
        if (controller == IntPtr.Zero)
        {
            _previous = GamepadButtons.None;
            return GamepadState.None;
        }

        IntPtr pad = Send<IntPtr>(controller, SelExtended);
        GamepadButtons held = GamepadButtons.None;
        void Button(string name, GamepadButtons flag)
        {
            IntPtr selector = Sel(name);
            if (Send<byte, IntPtr>(pad, SelResponds, selector) == 0) return;
            IntPtr element = Send<IntPtr>(pad, selector);
            if (element != IntPtr.Zero && Send<byte>(element, SelPressed) != 0) held |= flag;
        }
        Button("buttonA", GamepadButtons.South);
        Button("buttonB", GamepadButtons.East);
        Button("buttonX", GamepadButtons.West);
        Button("buttonY", GamepadButtons.North);
        Button("leftShoulder", GamepadButtons.LeftShoulder);
        Button("rightShoulder", GamepadButtons.RightShoulder);
        Button("buttonMenu", GamepadButtons.Menu);
        Button("buttonOptions", GamepadButtons.Options);
        IntPtr dpad = Send<IntPtr>(pad, Sel("dpad"));
        if (Send<byte>(Send<IntPtr>(dpad, Sel("up")), SelPressed) != 0) held |= GamepadButtons.Up;
        if (Send<byte>(Send<IntPtr>(dpad, Sel("down")), SelPressed) != 0) held |= GamepadButtons.Down;
        if (Send<byte>(Send<IntPtr>(dpad, Sel("left")), SelPressed) != 0) held |= GamepadButtons.Left;
        if (Send<byte>(Send<IntPtr>(dpad, Sel("right")), SelPressed) != 0) held |= GamepadButtons.Right;

        IntPtr stick = Send<IntPtr>(pad, Sel("leftThumbstick"));
        var left = new Vector2(Send<float>(Send<IntPtr>(stick, SelX), SelValue), Send<float>(Send<IntPtr>(stick, SelY), SelValue));
        held |= GamepadState.StickButtons(left);

        GamepadButtons pressed = held & ~_previous;
        _previous = held;
        return new GamepadState(held, pressed) { Connected = true };
    }
}
