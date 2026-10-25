using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Cubicolor.Render;

public enum FontStyle
{
    Regular,
    Black,
}

// Text in the overlay: Vollkorn, rasterised by the platform at the exact pixel size it is drawn at and kept
// in the overlay atlas. Laid out on one line per call, with the font's own kerning; glyphs land on whole
// pixels so they stay crisp.
public sealed class Text : IDisposable
{
    private readonly IFontLoader _loader;
    private readonly string _directory;
    private readonly OverlayAtlas _atlas;
    private readonly Dictionary<(FontStyle, int), IFontFace> _faces = [];
    private readonly Dictionary<(FontStyle, int, char), Glyph?> _glyphs = [];
    private readonly Dictionary<(string, int), Glyph?> _symbols = [];

    // Draws a named symbol (on macOS an SF Symbol) at a pixel size, or null on platforms without them.
    public Func<string, float, GlyphInk?>? Symbols { get; set; }

    private sealed record Glyph(Vector2 UvTopLeft, Vector2 UvBottomRight, int Width, int Height, int Left, int Top);

    public Text(IFontLoader loader, string fontsDirectory, OverlayAtlas atlas)
    {
        _loader = loader;
        _directory = fontsDirectory;
        _atlas = atlas;
    }

    private IFontFace Face(FontStyle style, int pixels)
    {
        if (_faces.TryGetValue((style, pixels), out IFontFace? face)) return face;
        string file = style == FontStyle.Black ? "Vollkorn-Black.ttf" : "Vollkorn-Regular.ttf";
        face = _loader.Open(Path.Combine(_directory, file), pixels);
        _faces[(style, pixels)] = face;
        return face;
    }

    public float LineHeight(FontStyle style, float size)
    {
        IFontFace face = Face(style, Pixels(size));
        return face.Ascent + face.Descent;
    }

    public float Measure(string text, FontStyle style, float size)
    {
        IFontFace face = Face(style, Pixels(size));
        float width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            width += face.Advance(face.GlyphFor(text[i]));
            if (i + 1 < text.Length) width += face.Kerning(text[i], text[i + 1]);
        }
        return width;
    }

    // Draws `text` with the top of its line box at `topLeft`.
    public void Draw(Overlay overlay, string text, Vector2 topLeft, FontStyle style, float size, Vector4 color)
    {
        int pixels = Pixels(size);
        IFontFace face = Face(style, pixels);
        float baseline = MathF.Round(topLeft.Y + face.Ascent);
        float pen = topLeft.X;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (GlyphOf(face, style, pixels, c) is { } glyph)
            {
                float x = MathF.Round(pen) + glyph.Left;
                float y = baseline - glyph.Top;
                overlay.Image(new Rect(x, y, glyph.Width, glyph.Height), glyph.UvTopLeft, glyph.UvBottomRight, color);
            }
            pen += face.Advance(face.GlyphFor(c));
            if (i + 1 < text.Length) pen += face.Kerning(c, text[i + 1]);
        }
    }

    private Glyph? GlyphOf(IFontFace face, FontStyle style, int pixels, char c)
    {
        if (_glyphs.TryGetValue((style, pixels, c), out Glyph? glyph)) return glyph;
        GlyphInk? ink = face.Rasterize(face.GlyphFor(c));
        if (ink != null)
        {
            if (!_atlas.Fits(ink.Width, ink.Height))
            {
                // Sizes come and go with the window: start the atlas over rather than fill it for good.
                _atlas.Clear();
                _glyphs.Clear();
                _symbols.Clear();
            }
            var (topLeft, bottomRight) = _atlas.Add(ink.Width, ink.Height, ink.Coverage);
            glyph = new Glyph(topLeft, bottomRight, ink.Width, ink.Height, ink.Left, ink.Top);
        }
        _glyphs[(style, pixels, c)] = glyph;
        return glyph;
    }

    // A named symbol, `size` pixels high, centred on `centre`; false when there is no such symbol to draw.
    public bool DrawSymbol(Overlay overlay, string name, Vector2 centre, float size, Vector4 color)
    {
        int pixels = Pixels(size);
        if (!_symbols.TryGetValue((name, pixels), out Glyph? glyph))
        {
            GlyphInk? ink = Symbols?.Invoke(name, pixels);
            if (ink != null)
            {
                if (!_atlas.Fits(ink.Width, ink.Height))
                {
                    _atlas.Clear();
                    _glyphs.Clear();
                    _symbols.Clear();
                }
                var (topLeft, bottomRight) = _atlas.Add(ink.Width, ink.Height, ink.Coverage);
                glyph = new Glyph(topLeft, bottomRight, ink.Width, ink.Height, 0, 0);
            }
            _symbols[(name, pixels)] = glyph;
        }
        if (glyph == null) return false;
        float x = MathF.Round(centre.X - glyph.Width / 2f), y = MathF.Round(centre.Y - glyph.Height / 2f);
        overlay.Image(new Rect(x, y, glyph.Width, glyph.Height), glyph.UvTopLeft, glyph.UvBottomRight, color);
        return true;
    }

    private static int Pixels(float size) => Math.Max(4, (int) MathF.Round(size));

    public void Dispose()
    {
        foreach (IFontFace face in _faces.Values) face.Dispose();
        _faces.Clear();
    }
}
