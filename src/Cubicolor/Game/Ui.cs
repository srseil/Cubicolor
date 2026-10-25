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
    private string? _dragging;
    private bool _mouseMoved;
    private Vector2 _lastMouse;

    public Overlay Overlay { get; } = new();
    // Drawn before the 3D scene, under it: the menus' panels, so a scene can be shown on one.
    public Overlay Underlay { get; } = new();
    // The UI is laid out on a 1280 x 720 design canvas, scaled to fit the window whole and centred in it, so
    // nothing is ever cut off: Scale is pixels per design unit, Origin the canvas's top left in pixels.
    public const float CanvasWidth = 1280, CanvasHeight = 720;
    public float Scale { get; private set; } = 1;
    public Vector2 Origin { get; private set; }
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

    public void Begin(IInput input, Navigation navigation, int width, int height, double seconds)
    {
        _seconds = seconds;
        Interactive = true;
        Input = input;
        Navigation = navigation;
        Size = new Vector2(width, height);
        Scale = MathF.Min(height / CanvasHeight, width / CanvasWidth);
        Origin = new Vector2((width - CanvasWidth * Scale) / 2, (height - CanvasHeight * Scale) / 2);
        Overlay.Clear();
        Underlay.Clear();
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

    // A rectangle and a point on the design canvas, in pixels.
    public Rect R(float x, float y, float width, float height) => new(Origin.X + Px(x), Origin.Y + Px(y), Px(width), Px(height));

    public Vector2 P(float x, float y) => new(Origin.X + Px(x), Origin.Y + Px(y));

    // Back from pixels to the design canvas.
    public float DesignX(float pixels) => (pixels - Origin.X) / Scale;
    public float DesignY(float pixels) => (pixels - Origin.Y) / Scale;

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
    // While false, widgets draw but cannot be focused or used: a panel fading out.
    public bool Interactive { get; set; } = true;

    private double _seconds;
    // How much of each widget's focus ring shows: it fades in and out over a tenth of a second.
    private readonly Dictionary<string, float> _ring = [];
    private float _ringNow;
    private Rect _hit;   // the last widget's hit area

    // A widget answers to the mouse a little beyond what it draws (`hit`, by default its rectangle grown by 5
    // units on every side), so the gaps between neighbours are not dead space.
    private (bool Focused, bool Activated) Interact(string label, Rect rect, bool enabled, Rect? hit = null)
    {
        _ringNow = 0;
        if (!enabled || !Interactive) return (false, false);
        _widgets.Add((label, rect));
        _hit = hit ?? rect.Inset(-Px(5));
        bool over = _hit.Contains(Input.MousePosition);
        if (over && _mouseMoved) _focus = label;
        bool focused = _focus == label;
        float ring = _ring.GetValueOrDefault(label);
        ring = Math.Clamp(ring + (focused ? 1 : -1) * (float) _seconds / 0.1f, 0, 1);
        _ring[label] = ring;
        _ringNow = ring;
        bool activated = (over && Input.MouseReleased(MouseButton.Left)) || (focused && Navigation.Confirm);
        if (activated) Clicked = true;
        return (focused, activated);
    }

    // The ring around whatever has focus: its own colour, so it never reads as selected or finished, which
    // are shown by the khaki fill and the khaki tick.
    private void FocusRing(Rect rect, float radius)
    {
        if (_ringNow <= 0) return;
        Vector4 color = Dark ? Palette.DarkText : Style.Dark;
        Overlay.RoundedOutline(new Rect(rect.X - Px(3), rect.Y - Px(3), rect.Width + Px(6), rect.Height + Px(6)), radius + Px(3), Px(2.5f),
            color with { W = color.W * _ringNow });
    }

    // `selected` fills it khaki (the current section or difficulty); `done` puts a khaki tick in its corner (a
    // solved level).
    public bool Button(string label, Rect rect, float textSize = 24, bool enabled = true, bool selected = false, string? id = null,
                       bool done = false, Rect? hit = null)
    {
        var (focused, activated) = Interact(id ?? label, rect, enabled, hit);
        float radius = Px(5);
        if (!enabled)
        {
            Overlay.RoundedRectangle(rect, radius, Style.Disabled);
            Label(label, rect, textSize, Style.DisabledText);
            return false;
        }
        // Pressed while the mouse holds it, and on the frame it lets go too: that is when the click lands and the
        // button may become the selected one, which it otherwise only shows a frame later.
        bool pressed = focused && _hit.Contains(Input.MousePosition) && (Input.MouseDown(MouseButton.Left) || activated && Input.MouseReleased(MouseButton.Left));
        FocusRing(rect, radius);
        Overlay.RoundedRectangle(rect, radius, pressed || selected ? Style.Accent : Style.Button);
        if (done)
        {
            float tick = MathF.Min(rect.Width, rect.Height) * 0.36f;
            Tick(new Rect(rect.Right - tick - Px(2), rect.Y + Px(2), tick, tick), Style.Accent);
        }
        Label(label, rect, textSize, pressed || selected ? Style.Dark : Style.ButtonText);
        return activated;
    }

    public bool Toggle(string label, Rect rect, ref bool value, float textSize = 24, string? id = null)
    {
        var (focused, activated) = Interact(id ?? label, rect, true);
        if (activated) value = !value;
        float box = rect.Height * 0.7f;
        var boxRect = new Rect(rect.X, rect.Y + (rect.Height - box) / 2, box, box);
        FocusRing(boxRect, Px(5));
        Overlay.RoundedRectangle(boxRect, Px(5), Style.Button);
        if (value) Tick(boxRect, Style.ButtonText);
        Label(label, new Vector2(boxRect.Right + Px(12), rect.Y + (rect.Height - _text.LineHeight(FontStyle.Regular, Px(textSize))) / 2), textSize);
        return activated;
    }

    // Undoes this frame's navigation and leaves nothing to be activated: while a binding is being captured, the
    // key or button pressed for it must do nothing else.
    public void Hold()
    {
        _focus = FocusBefore;
        Navigation = default;
        Interactive = false;
    }

    // A button showing symbols (a controller's own, for one of its buttons) instead of a label, or `text` when
    // there are none.
    public bool SymbolButton(string id, Rect rect, IReadOnlyList<string> symbols, string text, bool enabled = true)
    {
        var (focused, activated) = Interact(id, rect, enabled);
        float radius = Px(5);
        FocusRing(rect, radius);
        Overlay.RoundedRectangle(rect, radius, enabled ? Style.Button : Style.Disabled);
        Vector4 color = enabled ? Style.ButtonText : Style.DisabledText;
        float size = rect.Height * 0.6f, gap = Px(10);
        float x = rect.Centre.X - (symbols.Count * size + (symbols.Count - 1) * gap) / 2 + size / 2;
        bool drawn = symbols.Count > 0;
        foreach (string symbol in symbols)
        {
            drawn &= _text.DrawSymbol(Overlay, symbol, new Vector2(x, rect.Centre.Y), size, color);
            x += size + gap;
        }
        if (!drawn) Label(text, rect, 22, color);
        return activated;
    }

    // A web link: text underlined, activated like a button.
    public bool Link(string text, Rect rect, float textSize = 24)
    {
        var (focused, activated) = Interact(text, rect, true);
        FocusRing(rect, Px(5));
        float width = _text.Measure(text, FontStyle.Regular, Px(textSize));
        float height = _text.LineHeight(FontStyle.Regular, Px(textSize));
        var at = new Vector2(rect.X + (rect.Width - width) / 2, rect.Y + (rect.Height - height) / 2);
        Label(text, at, textSize);
        Overlay.Rectangle(new Rect(at.X, at.Y + height * 0.92f, width, MathF.Max(1, Px(1.5f))), TextColor);
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
    // A disabled slider shows its value greyed and cannot be focused or moved.
    public bool Slider(string id, Rect rect, ref double value, Rect? row = null, bool enabled = true)
    {
        if (!enabled)
        {
            Overlay.RoundedRectangle(rect, Px(6), Style.Track);
            Overlay.RoundedRectangle(new Rect(rect.X, rect.Y, MathF.Max(rect.Height, rect.Width * (float) value), rect.Height), Px(6), Style.Disabled);
            return false;
        }
        var (focused, _) = Interact(id, row ?? rect, true);
        _sliders.Add(id);
        double before = value;
        // Pressed on the track, it follows the mouse until the button is let go, wherever the mouse goes, so
        // both ends are easy to reach.
        if (Input.MousePressed(MouseButton.Left) && rect.Inset(-Px(6)).Contains(Input.MousePosition)) _dragging = id;
        if (_dragging == id)
        {
            value = Math.Clamp((Input.MousePosition.X - rect.X) / rect.Width, 0, 1);
            if (!Input.MouseDown(MouseButton.Left)) _dragging = null;
        }
        if (focused && Navigation.Move == Direction.Left) value = Math.Max(0, Math.Round(value * 10 - 1) / 10);
        if (focused && Navigation.Move == Direction.Right) value = Math.Min(1, Math.Round(value * 10 + 1) / 10);
        FocusRing(rect, Px(6));
        Overlay.RoundedRectangle(rect, Px(6), Style.Track);
        Overlay.RoundedRectangle(new Rect(rect.X, rect.Y, MathF.Max(rect.Height, rect.Width * (float) value), rect.Height), Px(6), Style.Accent);
        return Math.Abs(before - value) > 1e-9;
    }

    // A panel behind a group of widgets.
    // A panel behind a group of widgets, outlined all round; `under` puts it beneath the 3D scene.
    public void Panel(Rect rect, bool under = false)
    {
        float radius = Px(5);
        Overlay target = under ? Underlay : Overlay;
        target.RoundedRectangle(rect, radius, Dark ? Palette.Hex(0x4a4a4a) : Style.Panel);
        target.RoundedOutline(rect, radius, Px(2.5f), Style.Dark);
    }
}
