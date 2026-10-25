using System;
using System.Collections.Generic;
using System.Linq;
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
        LearnSymbols();
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

    // ---- the buttons' symbols ----

    // Every connected controller's SF Symbols name for each button, as GameController gives it
    // (GCControllerElement.unmappedSfSymbolsName: the button's own symbol, for a game's remapping screen).
    // Relearned whenever the set of controllers changes.
    private static IntPtr[] _known = [];
    private static readonly List<Dictionary<GamepadButtons, string>> _symbols = [];
    // The names (GCControllerElement.localizedName) of buttons whose symbol another button on the same
    // controller shares.
    private static readonly Dictionary<GamepadButtons, string> _ambiguous = [];

    public static string? NameFor(GamepadButtons button) => _ambiguous.GetValueOrDefault(button);

    // The distinct symbols for `button` across the connected controllers; with none connected (or none naming
    // it), the Xbox layout's symbol, so the controls screen shows an icon either way.
    public static IReadOnlyList<string> SymbolsFor(GamepadButtons button)
    {
        var names = new List<string>(2);
        foreach (Dictionary<GamepadButtons, string> controller in _symbols)
            if (controller.TryGetValue(button, out string? name) && !names.Contains(name)) names.Add(name);
        if (names.Count == 0 && Defaults.TryGetValue(button, out string? fallback)) names.Add(fallback);
        return names;
    }

    private static readonly Dictionary<GamepadButtons, string> Defaults = new()
    {
        [GamepadButtons.South] = "a.circle", [GamepadButtons.East] = "b.circle",
        [GamepadButtons.West] = "x.circle", [GamepadButtons.North] = "y.circle",
        [GamepadButtons.LeftShoulder] = "lb.rectangle.roundedbottom", [GamepadButtons.RightShoulder] = "rb.rectangle.roundedbottom",
        [GamepadButtons.Menu] = "line.3.horizontal.circle", [GamepadButtons.Options] = "rectangle.on.rectangle.circle",
        [GamepadButtons.Up] = "dpad.up.filled", [GamepadButtons.Down] = "dpad.down.filled",
        [GamepadButtons.Left] = "dpad.left.filled", [GamepadButtons.Right] = "dpad.right.filled",
    };

    private static readonly (GamepadButtons Button, string[] Path)[] Elements =
    [
        (GamepadButtons.South, ["buttonA"]), (GamepadButtons.East, ["buttonB"]),
        (GamepadButtons.West, ["buttonX"]), (GamepadButtons.North, ["buttonY"]),
        (GamepadButtons.LeftShoulder, ["leftShoulder"]), (GamepadButtons.RightShoulder, ["rightShoulder"]),
        (GamepadButtons.Menu, ["buttonMenu"]), (GamepadButtons.Options, ["buttonOptions"]),
        (GamepadButtons.Up, ["dpad", "up"]), (GamepadButtons.Down, ["dpad", "down"]),
        (GamepadButtons.Left, ["dpad", "left"]), (GamepadButtons.Right, ["dpad", "right"]),
    ];

    private static void LearnSymbols()
    {
        IntPtr all = Send<IntPtr>(_class, SelControllers);
        int count = (int) Send<nuint>(all, SelCount);
        var current = new IntPtr[count];
        for (int i = 0; i < count; i++) current[i] = Send<IntPtr, nuint>(all, SelAt, (nuint) i);
        if (current.AsSpan().SequenceEqual(_known)) return;
        _known = current;
        _symbols.Clear();
        _ambiguous.Clear();
        foreach (IntPtr controller in current)
        {
            IntPtr pad = Send<IntPtr>(controller, SelExtended);
            if (pad == IntPtr.Zero) continue;
            var map = new Dictionary<GamepadButtons, string>();
            var names = new Dictionary<GamepadButtons, string>();
            foreach (var (button, path) in Elements)
            {
                IntPtr element = pad;
                foreach (string name in path)
                {
                    IntPtr selector = Sel(name);
                    element = element == IntPtr.Zero || Send<byte, IntPtr>(element, SelResponds, selector) == 0 ? IntPtr.Zero : Send<IntPtr>(element, selector);
                }
                if (element == IntPtr.Zero || Send<byte, IntPtr>(element, SelResponds, Sel("unmappedSfSymbolsName")) == 0) continue;
                if (Str(Send<IntPtr>(element, Sel("unmappedSfSymbolsName"))) is { Length: > 0 } symbol) map.TryAdd(button, symbol);
                if (Send<byte, IntPtr>(element, SelResponds, Sel("localizedName")) != 0
                    && Str(Send<IntPtr>(element, Sel("localizedName"))) is { Length: > 0 } localized) names.TryAdd(button, localized.EndsWith(" Button", StringComparison.Ordinal) ? localized[..^7] : localized);
            }
            _symbols.Add(map);
            foreach (var (button, symbol) in map)
                if (map.Count(kv => kv.Value == symbol) > 1 && names.TryGetValue(button, out string? shown)) _ambiguous.TryAdd(button, shown);
            if (_ambiguous.Count > 0) Log.Info($"Game controller buttons named, their symbols being alike: {string.Join(", ", _ambiguous.Values)}");
            Log.Info($"Game controller symbols, {Str(Send<IntPtr>(controller, SelVendor)) ?? "unnamed"}: "
                     + (map.Count == 0 ? "none" : string.Join(", ", map.Select(kv => $"{kv.Key} {kv.Value}"))));
        }
    }
}
