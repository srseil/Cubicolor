namespace Cubicolor.Simulation.Input;

// A game controller's button, named for the Xbox layout's positions (South is A on an Xbox pad, Cross on a
// PlayStation one; Menu the right of the two middle buttons), as a binding stores it. The order matches the
// platform's GamepadButtons flags: button b is the flag 1 << b.
public enum PadButton
{
    South,
    East,
    West,
    North,
    LeftShoulder,
    RightShoulder,
    Menu,
    Options,
    Up,
    Down,
    Left,
    Right,
}
