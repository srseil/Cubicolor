using System;
using System.Collections.Generic;
using System.Numerics;
using Cubicolor.Platform;
using Cubicolor.Render;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.Game;

// What the UI draws with, in the earlier versions' style: dark rounded buttons with light text, a khaki line
// under the one in focus, light panels with a dark bottom edge.
public static class Style
{
    public static readonly Vector4 Button = Palette.Hex(0x2d2d2d);
    public static readonly Vector4 ButtonText = Palette.Hex(0xd9d9d9);
    public static readonly Vector4 Accent = Palette.Hex(0xa09f93);
    public static readonly Vector4 Panel = Palette.Hex(0xe8e6df);
    public static readonly Vector4 Track = Palette.Hex(0xf2f0ec);
    public static readonly Vector4 Dark = Palette.Hex(0x2d2d2d);
    public static readonly Vector4 Disabled = Palette.Hex(0xd4d2cc);
    public static readonly Vector4 DisabledText = Palette.Hex(0xa8a69f);
}

// The player's intent this frame, from keyboard, mouse and controller alike.
public readonly record struct Navigation(Direction? Move, bool Confirm, bool Back);

// A small immediate-mode UI. Each frame, screens call the widget methods in drawing order; each widget draws
// itself and says whether it was activated. One widget has focus: the mouse gives it to whatever it is over,
// and the arrow keys, d-pad or stick move it to the nearest widget in that direction, so every menu works
// with a controller. Sizes are in design units: 1 is a pixel of a 720 pixel high window, scaled to the real
// height.
public sealed class Ui
{
    private readonly Text _text;
    private readonly List<(string Label, Rect Rect)> _widgets = [];
    private List<(string Label, Rect Rect)> _previous = [];
    private string? _focus;
    private HashSet<string> _sliders = [], _previousSliders = [];
    private bool _mouseMoved;
    private Vector2 _lastMouse;

    public Overlay Overlay { get; } = new();
    public float Scale { get; private set; } = 1;
    public Vector2 Size { get; private set; }
    public IInput Input { get; private set; } = null!;
    public Navigation Navigation { get; private set; }
    public bool Dark { get; set; }

    // Set when a widget was activated this frame, for the click sound.
    public bool Clicked { get; private set; }

    // The widgets drawn last frame, by label: what a script can click.
    public IReadOnlyList<(string Label, Rect Rect)> Widgets => _previous;

    public Ui(Text text) => _text = text;

    public Vector4 TextColor => Dark ? Palette.DarkText : Palette.Text;

    public void Begin(IInput input, Navigation navigation, int width, int height)
    {
        Input = input;
        Navigation = navigation;
        Size = new Vector2(width, height);
        Scale = height / 720f;
        Overlay.Clear();
        Clicked = false;
        _previous = [.. _widgets];
        _widgets.Clear();
        (_previousSliders, _sliders) = (_sliders, _previousSliders);
        _sliders.Clear();
        _mouseMoved = Vector2.DistanceSquared(input.MousePosition, _lastMouse) > 0.5f;
        _lastMouse = input.MousePosition;

        FocusBefore = _focus;
        // Left and right on a slider move the slider, not the focus.
        bool onSlider = _focus != null && _previousSliders.Contains(_focus);
        if (navigation.Move is { } direction && _previous.Count > 0 && !(onSlider && direction is Direction.Left or Direction.Right))
        {
            if (_focus == null || !_previous.Exists(w => w.Label == _focus)) _focus = _previous[0].Label;
            else _focus = Nearest(direction) ?? _focus;
        }
    }

    // Puts focus on a widget by label (a screen opening a panel), if nothing has it yet.
    public void Focus(string label, bool force = false)
    {
        if (force || _focus == null) _focus = label;
    }

    public string? Focused => _focus;

    // What had focus before this frame's navigation moved it.
    public string? FocusBefore { get; private set; }

    private string? Nearest(Direction direction)
    {
        Rect from = _previous.Find(w => w.Label == _focus).Rect;
        Vector2 origin = from.Centre;
        string? best = null;
        float bestScore = float.MaxValue;
        foreach (var (label, rect) in _previous)
        {
            if (label == _focus) continue;
            Vector2 d = rect.Centre - origin;
            float along = direction switch
            {
                Direction.Forwards => -d.Y,
                Direction.Backwards => d.Y,
                Direction.Left => -d.X,
                _ => d.X,
            };
            float across = direction is Direction.Forwards or Direction.Backwards ? MathF.Abs(d.X) : MathF.Abs(d.Y);
            if (along <= 1f) continue;
            float score = along + across * 2;
            if (score < bestScore)
            {
                bestScore = score;
                best = label;
            }
        }
        return best;
    }

    public float Px(float units) => units * Scale;

    public Rect R(float x, float y, float width, float height) => new(Px(x), Px(y), Px(width), Px(height));

    public Vector2 P(float x, float y) => new(Px(x), Px(y));

    // --- Text ---

    public float TextWidth(string text, float size, FontStyle style = FontStyle.Regular) => _text.Measure(text, style, Px(size));

    public void Label(string text, Vector2 position, float size, Vector4? color = null, FontStyle style = FontStyle.Regular) =>
        _text.Draw(Overlay, text, position, style, Px(size), color ?? TextColor);

    // Text centred in a rectangle.
    public void Label(string text, Rect rect, float size, Vector4? color = null, FontStyle style = FontStyle.Regular)
    {
        float width = _text.Measure(text, style, Px(size));
        float height = _text.LineHeight(style, Px(size));
        _text.Draw(Overlay, text, new Vector2(rect.X + (rect.Width - width) / 2, rect.Y + (rect.Height - height) / 2), style, Px(size), color ?? TextColor);
    }

    // --- Widgets ---

    // A widget's interaction: registered for focus and scripts, focused by the mouse, activated by a click
    // or by Confirm while focused.
    private (bool Focused, bool Activated) Interact(string label, Rect rect, bool enabled)
    {
        if (!enabled) return (false, false);
        _widgets.Add((label, rect));
        bool over = rect.Contains(Input.MousePosition);
        if (over && _mouseMoved) _focus = label;
        bool focused = _focus == label;
        bool activated = (over && Input.MouseReleased(MouseButton.Left)) || (focused && Navigation.Confirm);
        if (activated) Clicked = true;
        return (focused, activated);
    }

    // The ring around whatever has focus: its own colour, so it never reads as selected or finished, which
    // are shown by the khaki fill and the khaki tick.
    private void FocusRing(Rect rect, float radius) =>
        Overlay.RoundedOutline(new Rect(rect.X - Px(3), rect.Y - Px(3), rect.Width + Px(6), rect.Height + Px(6)), radius + Px(3), Px(2.5f),
            Dark ? Palette.DarkText : Style.Dark);

    // `selected` fills it khaki (the current section or difficulty); `done` puts a khaki tick in its corner (a
    // solved level).
    public bool Button(string label, Rect rect, float textSize = 24, bool enabled = true, bool selected = false, string? id = null,
                       bool done = false)
    {
        var (focused, activated) = Interact(id ?? label, rect, enabled);
        float radius = Px(5);
        if (!enabled)
        {
            Overlay.RoundedRectangle(rect, radius, Style.Disabled);
            Label(label, rect, textSize, Style.DisabledText);
            return false;
        }
        bool pressed = focused && rect.Contains(Input.MousePosition) && Input.MouseDown(MouseButton.Left);
        if (focused) FocusRing(rect, radius);
        Overlay.RoundedRectangle(rect, radius, pressed || selected ? Style.Accent : Style.Button);
        if (done)
        {
            float tick = MathF.Min(rect.Width, rect.Height) * 0.36f;
            Tick(new Rect(rect.Right - tick - Px(2), rect.Y + Px(2), tick, tick), Style.Accent);
        }
        Label(label, rect, textSize, pressed || selected ? Style.Dark : Style.ButtonText);
        return activated;
    }

    public bool Toggle(string label, Rect rect, ref bool value, float textSize = 24)
    {
        var (focused, activated) = Interact(label, rect, true);
        if (activated) value = !value;
        float box = rect.Height * 0.7f;
        var boxRect = new Rect(rect.X, rect.Y + (rect.Height - box) / 2, box, box);
        if (focused) FocusRing(boxRect, Px(5));
        Overlay.RoundedRectangle(boxRect, Px(5), Style.Button);
        if (value) Tick(boxRect, Style.ButtonText);
        Label(label, new Vector2(boxRect.Right + Px(12), rect.Y + (rect.Height - _text.LineHeight(FontStyle.Regular, Px(textSize))) / 2), textSize);
        return activated;
    }

    // A check mark across a box: two strokes, short down and long up.
    private void Tick(Rect box, Vector4 color)
    {
        float w = box.Width;
        Vector2 a = new(box.X + w * 0.24f, box.Y + w * 0.52f), b = new(box.X + w * 0.43f, box.Y + w * 0.72f), c = new(box.X + w * 0.78f, box.Y + w * 0.28f);
        Stroke(a, b, w * 0.13f, color);
        Stroke(b, c, w * 0.13f, color);
    }

    private void Stroke(Vector2 from, Vector2 to, float thickness, Vector4 color)
    {
        Vector2 along = Vector2.Normalize(to - from);
        Vector2 side = new Vector2(-along.Y, along.X) * thickness / 2;
        from -= along * thickness / 2;
        to += along * thickness / 2;
        Overlay.Triangle(from - side, to - side, to + side, color);
        Overlay.Triangle(from - side, to + side, from + side, color);
    }

    // A volume slider from 0 to 1: dragged with the mouse, or moved in tenths with left and right while in
    // focus (which then does not move focus away).
    // `row`, when given, is the whole line it sits on (its label included): what focus moves between, so a
    // column of settings lines up.
    public bool Slider(string id, Rect rect, ref double value, Rect? row = null)
    {
        var (focused, _) = Interact(id, row ?? rect, true);
        _sliders.Add(id);
        double before = value;
        if (focused && rect.Contains(Input.MousePosition) && Input.MouseDown(MouseButton.Left))
            value = Math.Clamp((Input.MousePosition.X - rect.X) / rect.Width, 0, 1);
        if (focused && Navigation.Move == Direction.Left) value = Math.Max(0, Math.Round(value * 10 - 1) / 10);
        if (focused && Navigation.Move == Direction.Right) value = Math.Min(1, Math.Round(value * 10 + 1) / 10);
        if (focused) FocusRing(rect, Px(6));
        Overlay.RoundedRectangle(rect, Px(6), Style.Track);
        Overlay.RoundedRectangle(new Rect(rect.X, rect.Y, MathF.Max(rect.Height, rect.Width * (float) value), rect.Height), Px(6), Style.Accent);
        return Math.Abs(before - value) > 1e-9;
    }

    // A panel behind a group of widgets.
    public void Panel(Rect rect)
    {
        float radius = Px(5);
        Overlay.RoundedRectangle(new Rect(rect.X, rect.Y + Px(3), rect.Width, rect.Height), radius, Style.Dark);
        Overlay.RoundedRectangle(rect, radius, Dark ? Palette.Hex(0x4a4a4a) : Style.Panel);
    }
}
