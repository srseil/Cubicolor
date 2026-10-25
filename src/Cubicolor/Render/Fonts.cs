using System;

namespace Cubicolor.Render;

// A glyph's coverage, 8 bits a pixel, cropped to its ink. `Left` is the x of its left edge from the pen,
// `Top` the y of its top edge above the baseline, both in pixels.
public sealed record GlyphInk(int Width, int Height, byte[] Coverage, int Left, int Top);

// One font file at one pixel size, as the platform's own text system reads and draws it (Core Text, DirectWrite,
// FreeType): metrics in pixels, glyphs by character, the font's own kerning, and each glyph's coverage.
public interface IFontFace : IDisposable
{
    float Ascent { get; }
    float Descent { get; }   // positive, below the baseline
    float LineGap { get; }
    ushort GlyphFor(char c);
    float Advance(ushort glyph);
    float Kerning(char left, char right);
    GlyphInk? Rasterize(ushort glyph);
}

public interface IFontLoader
{
    IFontFace Open(string path, float pixelSize);

    // A font installed with the system, by name, or null when there is none of that name.
    IFontFace? OpenInstalled(string name, float pixelSize);
}
