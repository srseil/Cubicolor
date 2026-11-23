using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cubicolor.Render;

namespace Cubicolor.Platform.Windows;

// A font file opened through DirectWrite, at one pixel size: the metrics, advances and kerning Text lays text
// out by, and each glyph's grayscale coverage. The file is read into a font collection of its own (a font set,
// Windows 10 1703 on), so a text layout can measure pairs with the font's own kerning, as Core Text's CTLine does
// on macOS. DirectWrite reads OpenType (CFF and TrueType outlines) itself, so there is no font parser here.
//
// Sizes are in pixels, one per DIP; y runs DOWN from the baseline, as DirectWrite has it.
internal sealed unsafe partial class DirectWriteFont : IFontFace
{
    private static IntPtr _factory;

    private IntPtr _face, _format;
    private readonly float _size;
    private readonly float _scale;   // pixels per design unit

    public float Ascent { get; }
    public float Descent { get; }   // positive, below the baseline
    public float LineGap { get; }

    private readonly Dictionary<char, ushort> _glyphs = [];
    private readonly Dictionary<ushort, float> _advances = [];
    private readonly Dictionary<(char, char), float> _kerning = [];

    public DirectWriteFont(string path, float size)
    {
        _size = size;
        IntPtr factory = Factory();
        IntPtr file = IntPtr.Zero, builder = IntPtr.Zero, set = IntPtr.Zero, collection = IntPtr.Zero, family = IntPtr.Zero,
            font = IntPtr.Zero, names = IntPtr.Zero;
        try
        {
            // IDWriteFactory::CreateFontFileReference (7)
            fixed (char* p = path)
                Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr, IntPtr*, int>) Com.Slot(factory, 7))(factory, p, IntPtr.Zero, &file),
                    $"Reading the font '{path}'");
            // IDWriteFactory5::CreateFontSetBuilder (43), IDWriteFontSetBuilder1::AddFontFile (7), CreateFontSet (6),
            // IDWriteFactory3::CreateFontCollectionFromFontSet (37)
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>) Com.Slot(factory, 43))(factory, &builder), "CreateFontSetBuilder");
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>) Com.Slot(builder, 7))(builder, file), $"Reading a font from '{path}'");
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>) Com.Slot(builder, 6))(builder, &set), "CreateFontSet");
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>) Com.Slot(factory, 37))(factory, set, &collection),
                "CreateFontCollectionFromFontSet");
            // IDWriteFontCollection::GetFontFamily (4), IDWriteFontList::GetFont (5), IDWriteFont::CreateFontFace (13)
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>) Com.Slot(collection, 4))(collection, 0, &family),
                $"No font in '{path}'");
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>) Com.Slot(family, 5))(family, 0, &font), "GetFont");
            IntPtr face;
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>) Com.Slot(font, 13))(font, &face), "CreateFontFace");
            _face = face;

            // The text format kerning is measured with: the family by its name in this collection alone, in the
            // font's own weight, style and stretch (IDWriteFont::GetWeight 4, GetStretch 5, GetStyle 6).
            int weight = ((delegate* unmanaged[Stdcall]<IntPtr, int>) Com.Slot(font, 4))(font);
            int stretch = ((delegate* unmanaged[Stdcall]<IntPtr, int>) Com.Slot(font, 5))(font);
            int style = ((delegate* unmanaged[Stdcall]<IntPtr, int>) Com.Slot(font, 6))(font);
            // IDWriteFontFamily::GetFamilyNames (6), IDWriteLocalizedStrings::GetStringLength (7), GetString (8)
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>) Com.Slot(family, 6))(family, &names), "GetFamilyNames");
            uint length;
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, uint*, int>) Com.Slot(names, 7))(names, 0, &length), "GetStringLength");
            char* name = stackalloc char[(int) length + 1];
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, char*, uint, int>) Com.Slot(names, 8))(names, 0, name, length + 1), "GetString");
            // IDWriteFactory::CreateTextFormat (15)
            IntPtr format;
            fixed (char* locale = "en-us")
                Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr, int, int, int, float, char*, IntPtr*, int>) Com.Slot(factory, 15))(
                    factory, name, collection, weight, style, stretch, size, locale, &format), "CreateTextFormat");
            _format = format;
        }
        finally
        {
            Com.Release(ref names);
            Com.Release(ref font);
            Com.Release(ref family);
            Com.Release(ref collection);
            Com.Release(ref set);
            Com.Release(ref builder);
            Com.Release(ref file);
        }

        // IDWriteFontFace::GetMetrics (8): DWRITE_FONT_METRICS starts with designUnitsPerEm, ascent, descent (u16)
        // and lineGap (i16).
        byte* metrics = stackalloc byte[20];
        ((delegate* unmanaged[Stdcall]<IntPtr, byte*, void>) Com.Slot(_face, 8))(_face, metrics);
        _scale = size / *(ushort*) metrics;
        // The vertical metrics macOS uses (the hhea table's), so a line sits the same on both; DirectWrite's own
        // come from the OS/2 table's Windows ones, which often reach further.
        (float ascent, float descent, float gap) = Hhea() ?? (*(ushort*) (metrics + 2), *(ushort*) (metrics + 4), *(short*) (metrics + 6));
        (Ascent, Descent, LineGap) = (ascent * _scale, descent * _scale, gap * _scale);
    }

    // The hhea table's ascender, descender (as a positive depth) and line gap, in design units.
    private (float, float, float)? Hhea()
    {
        const uint tag = 'h' | 'h' << 8 | 'e' << 16 | 'a' << 24;
        byte* data;
        uint size;
        IntPtr context;
        int exists;
        // IDWriteFontFace::TryGetFontTable (12), ReleaseFontTable (13)
        if (((delegate* unmanaged[Stdcall]<IntPtr, uint, byte**, uint*, IntPtr*, int*, int>) Com.Slot(_face, 12))(_face, tag, &data, &size, &context, &exists) < 0
            || exists == 0) return null;
        try
        {
            if (size < 10) return null;
            var table = new ReadOnlySpan<byte>(data, (int) size);
            return (BinaryPrimitives.ReadInt16BigEndian(table[4..]), -BinaryPrimitives.ReadInt16BigEndian(table[6..]),
                BinaryPrimitives.ReadInt16BigEndian(table[8..]));
        }
        finally
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>) Com.Slot(_face, 13))(_face, context);
        }
    }

    private static IntPtr Factory()
    {
        if (_factory != IntPtr.Zero) return _factory;
        Guid iid = new("958DB99A-BE2A-4F09-AF7D-65189803D1D3");   // IDWriteFactory5
        IntPtr factory;
        Com.Check(DWriteCreateFactory(0 /* shared */, &iid, &factory), "DirectWrite (Windows 10 1703 or later)");
        _factory = factory;
        return factory;
    }

    // The glyph for a character, or 0 (the font's .notdef) when the font has none. Glyphs, advances and kerning
    // never change for a font: each is worked out once.
    public ushort GlyphFor(char c)
    {
        if (_glyphs.TryGetValue(c, out ushort cached)) return cached;
        uint codePoint = c;
        ushort glyph = 0;
        // IDWriteFontFace::GetGlyphIndices (11)
        ((delegate* unmanaged[Stdcall]<IntPtr, uint*, uint, ushort*, int>) Com.Slot(_face, 11))(_face, &codePoint, 1, &glyph);
        _glyphs[c] = glyph;
        return glyph;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphMetrics
    {
        public int LeftSideBearing;
        public uint AdvanceWidth;
        public int RightSideBearing, TopSideBearing;
        public uint AdvanceHeight;
        public int BottomSideBearing, VerticalOriginY;
    }

    public float Advance(ushort glyph)
    {
        if (_advances.TryGetValue(glyph, out float cached)) return cached;
        GlyphMetrics metrics;
        // IDWriteFontFace::GetDesignGlyphMetrics (10)
        ((delegate* unmanaged[Stdcall]<IntPtr, ushort*, uint, GlyphMetrics*, int, int>) Com.Slot(_face, 10))(_face, &glyph, 1, &metrics, 0);
        float advance = metrics.AdvanceWidth * _scale;
        _advances[glyph] = advance;
        return advance;
    }

    // How much the font's layout moves `right` when it follows `left`, beyond their own advances: DirectWrite's
    // layout of the pair, which applies the font's kerning, against the two advances alone.
    public float Kerning(char left, char right)
    {
        if (_kerning.TryGetValue((left, right), out float cached)) return cached;
        IntPtr factory = Factory(), layout;
        char* pair = stackalloc char[2] { left, right };
        // IDWriteFactory::CreateTextLayout (18), IDWriteTextLayout::GetMetrics (60)
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, uint, IntPtr, float, float, IntPtr*, int>) Com.Slot(factory, 18))(
            factory, pair, 2, _format, 1e6f, 1e6f, &layout), "CreateTextLayout");
        float* metrics = stackalloc float[9];   // DWRITE_TEXT_METRICS: left, top, width, widthIncludingTrailingWhitespace, ...
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, float*, int>) Com.Slot(layout, 60))(layout, metrics);
        Com.Release(ref layout);
        Com.Check(hr, "GetMetrics");
        float kerning = metrics[3] - Advance(GlyphFor(left)) - Advance(GlyphFor(right));
        _kerning[(left, right)] = kerning;
        return kerning;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphRun
    {
        public IntPtr FontFace;
        public float EmSize;
        public uint GlyphCount;
        public ushort* Indices;
        public float* Advances;
        public void* Offsets;
        public int IsSideways;
        public uint BidiLevel;
    }

    // A glyph's coverage, cropped to its ink; null for a glyph with none (space). Grayscale, unhinted and
    // antialiased both ways, which gives the outline's true coverage as Core Graphics does.
    public GlyphInk? Rasterize(ushort glyph)
    {
        IntPtr factory = Factory(), analysis;
        float advance = 0;
        var run = new GlyphRun { FontFace = _face, EmSize = _size, GlyphCount = 1, Indices = &glyph, Advances = &advance };
        // IDWriteFactory2::CreateGlyphRunAnalysis (30): natural symmetric rendering, natural measuring, no grid
        // fitting, grayscale.
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, GlyphRun*, void*, int, int, int, int, float, float, IntPtr*, int>) Com.Slot(factory, 30))(
            factory, &run, null, 5, 0, 1, 1, 0, 0, &analysis), "CreateGlyphRunAnalysis");
        try
        {
            // IDWriteGlyphRunAnalysis::GetAlphaTextureBounds (3), CreateAlphaTexture (4), aliased 1×1 being the
            // grayscale texture when the antialiasing is grayscale.
            Win32.Rect bounds;
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, int, Win32.Rect*, int>) Com.Slot(analysis, 3))(analysis, 0, &bounds), "GetAlphaTextureBounds");
            int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
            if (width <= 0 || height <= 0) return null;
            var pixels = new byte[width * height];
            fixed (byte* data = pixels)
                Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, int, Win32.Rect*, byte*, uint, int>) Com.Slot(analysis, 4))(
                    analysis, 0, &bounds, data, (uint) pixels.Length), "CreateAlphaTexture");
            return Crop(pixels, width, height, bounds.Left, bounds.Top);
        }
        finally
        {
            Com.Release(ref analysis);
        }
    }

    // Coverage in a `width` by `height` canvas whose top-left sits at (`left`, `top`) from the pen, y down,
    // cropped to its ink; null when nothing was drawn.
    private static GlyphInk? Crop(byte[] pixels, int width, int height, int left, int top)
    {
        int x0 = width, y0 = height, x1 = -1, y1 = -1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[y * width + x] != 0)
                {
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                    y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                }
        if (x1 < 0) return null;

        int iw = x1 - x0 + 1, ih = y1 - y0 + 1;
        var coverage = new byte[iw * ih];
        for (int y = 0; y < ih; y++)
            pixels.AsSpan((y0 + y) * width + x0, iw).CopyTo(coverage.AsSpan(y * iw));
        return new GlyphInk(iw, ih, coverage, left + x0, -(top + y0));
    }

    public void Dispose()
    {
        Com.Release(ref _format);
        Com.Release(ref _face);
    }

    [LibraryImport("dwrite.dll")] private static partial int DWriteCreateFactory(int type, Guid* iid, IntPtr* factory);
}

internal sealed class DirectWriteFontLoader : IFontLoader
{
    public IFontFace Open(string path, float pixelSize) => new DirectWriteFont(path, pixelSize);
}
