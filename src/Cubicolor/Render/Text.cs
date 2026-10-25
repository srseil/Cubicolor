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
    // Characters Vollkorn has no glyph for (Chinese, Japanese, Korean) come from the fallback fonts beside it,
    // tried in order; which character comes from which face is worked out once.
    private IReadOnlyList<string> _fallbacks = [];
    private readonly Dictionary<(string, int), IFontFace> _fallbackFaces = [];
    private readonly Dictionary<(FontStyle, int, char), IFontFace> _faceOf = [];
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

    // The font files (in the fonts directory) to draw what Vollkorn can't, in the order to try them.
    public IReadOnlyList<string> Fallbacks
    {
        get => _fallbacks;
        set
        {
            _fallbacks = value;
            _faceOf.Clear();
            _glyphs.Clear();
        }
    }

    // The face that draws `c` at this style and size: Vollkorn, or the first fallback that has it.
    private IFontFace FaceFor(FontStyle style, int pixels, IFontFace primary, char c)
    {
        if (_faceOf.TryGetValue((style, pixels, c), out IFontFace? face)) return face;
        face = primary;
        if (primary.GlyphFor(c) == 0 && !char.IsWhiteSpace(c))
            foreach (string file in _fallbacks)
            {
                if (!_fallbackFaces.TryGetValue((file, pixels), out IFontFace? fallback))
                    _fallbackFaces[(file, pixels)] = fallback = _loader.Open(Path.Combine(_directory, file), pixels);
                if (fallback.GlyphFor(c) != 0)
                {
                    face = fallback;
                    break;
                }
            }
        _faceOf[(style, pixels, c)] = face;
        return face;
    }

    public float LineHeight(FontStyle style, float size)
    {
        IFontFace face = Face(style, Pixels(size));
        return face.Ascent + face.Descent;
    }

    public float Measure(string text, FontStyle style, float size)
    {
        int pixels = Pixels(size);
        IFontFace primary = Face(style, pixels);
        float width = 0;
        for (int i = 0; i < text.Length; i++) width += Advance(style, pixels, primary, text, i);
        return width;
    }

    // How far the pen moves past text[i]: its advance, and Vollkorn's kerning before the next character when
    // Vollkorn draws both.
    private float Advance(FontStyle style, int pixels, IFontFace primary, string text, int i)
    {
        char c = text[i];
        IFontFace face = FaceFor(style, pixels, primary, c);
        float advance = face.Advance(face.GlyphFor(c));
        if (i + 1 < text.Length && face == primary && FaceFor(style, pixels, primary, text[i + 1]) == primary)
            advance += primary.Kerning(c, text[i + 1]);
        return advance;
    }

    // Draws `text` with the top of its line box at `topLeft`.
    public void Draw(Overlay overlay, string text, Vector2 topLeft, FontStyle style, float size, Vector4 color)
    {
        int pixels = Pixels(size);
        IFontFace primary = Face(style, pixels);
        float baseline = MathF.Round(topLeft.Y + primary.Ascent);
        float pen = topLeft.X;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (GlyphOf(FaceFor(style, pixels, primary, c), style, pixels, c) is { } glyph)
            {
                float x = MathF.Round(pen) + glyph.Left;
                float y = baseline - glyph.Top;
                overlay.Image(new Rect(x, y, glyph.Width, glyph.Height), glyph.UvTopLeft, glyph.UvBottomRight, color);
            }
            pen += Advance(style, pixels, primary, text, i);
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

    // An image of coverage (`ink`, made by the caller) drawn into `rect` at one texel a pixel, kept in the atlas
    // under `key` so it is uploaded once.
    public void DrawImage(Overlay overlay, string key, GlyphInk ink, Vector2 topLeft, Vector4 color)
    {
        if (!_images.TryGetValue(key, out Glyph? glyph))
        {
            if (!_atlas.Fits(ink.Width, ink.Height))
            {
                _atlas.Clear();
                _glyphs.Clear();
                _symbols.Clear();
                _images.Clear();
            }
            var (uvTopLeft, uvBottomRight) = _atlas.Add(ink.Width, ink.Height, ink.Coverage);
            glyph = new Glyph(uvTopLeft, uvBottomRight, ink.Width, ink.Height, 0, 0);
            _images[key] = glyph;
        }
        overlay.Image(new Rect(topLeft.X, topLeft.Y, glyph.Width, glyph.Height), glyph.UvTopLeft, glyph.UvBottomRight, color);
    }

    private readonly Dictionary<string, Glyph> _images = [];

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
        foreach (IFontFace face in _fallbackFaces.Values) face.Dispose();
        _fallbackFaces.Clear();
    }
}
