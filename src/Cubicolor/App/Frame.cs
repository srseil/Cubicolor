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

    public bool Run(IInput input, double seconds, int width, int height)
    {
        input.Poll();
        context.Ui.Dark = context.Saves.Settings.DarkBackground;
        context.Ui.Begin(input, Navigate(input), width, height);
        _scene.Items.Clear();
        Screen = Screen.Frame(seconds, _scene);
        return context.Renderer.Draw(_scene, context.Ui.Overlay.Vertices);
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
