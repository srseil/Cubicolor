using System;
using System.Runtime.InteropServices;
using System.Text;
using Cubicolor.Render;

namespace Cubicolor.Platform.Mac;

// A font file opened through macOS's Core Text, at one pixel size: the metrics, glyph bounds, advances and
// kerning SdfFont lays text out by, and each glyph's coverage drawn by Core Graphics (docs/native-macos.md
// §4.2). Core Text reads OpenType (CFF and TrueType outlines) itself, so there is no font parser here.
//
// Sizes are in pixels with one pixel per point; y runs UP from the baseline, as Core Text has it.
internal sealed unsafe partial class CoreTextFont : IFontFace
{
    private IntPtr _font;

    public float Ascent { get; }
    public float Descent { get; }   // positive, below the baseline
    public float LineGap { get; }

    public CoreTextFont(string path, double size)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(path);
        IntPtr url;
        fixed (byte* p = bytes) url = CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, p, bytes.Length, 0);
        if (url == IntPtr.Zero) throw new InvalidOperationException($"Couldn't make a URL of '{path}'");
        IntPtr descriptors = CTFontManagerCreateFontDescriptorsFromURL(url);
        CFRelease(url);
        if (descriptors == IntPtr.Zero || CFArrayGetCount(descriptors) == 0)
            throw new InvalidOperationException($"Couldn't read a font from '{path}'");
        _font = CTFontCreateWithFontDescriptor(CFArrayGetValueAtIndex(descriptors, 0), size, IntPtr.Zero);
        CFRelease(descriptors);
        if (_font == IntPtr.Zero) throw new InvalidOperationException($"Couldn't open the font in '{path}'");
        (Ascent, Descent, LineGap) = Metrics(_font);
    }

    private static (float, float, float) Metrics(IntPtr font) =>
        ((float) CTFontGetAscent(font), (float) CTFontGetDescent(font), (float) CTFontGetLeading(font));


    // The glyph for a character, or 0 (the font's .notdef) when the font has none.
    // Glyphs, advances and kerning are asked for every frame the same text is laid out, and Core Text's answers
    // never change for a font: each is worked out once. (Kerning builds a whole CTLine per pair.)
    private readonly System.Collections.Generic.Dictionary<char, ushort> _glyphs = [];
    private readonly System.Collections.Generic.Dictionary<ushort, float> _advances = [];
    private readonly System.Collections.Generic.Dictionary<(char, char), float> _kerning = [];

    public ushort GlyphFor(char c)
    {
        if (_glyphs.TryGetValue(c, out ushort cached)) return cached;
        ushort found = LookUpGlyph(c);
        _glyphs[c] = found;
        return found;
    }

    private ushort LookUpGlyph(char c)
    {
        ushort glyph = 0;
        CTFontGetGlyphsForCharacters(_font, &c, &glyph, 1);
        return glyph;
    }

    public float Advance(ushort glyph)
    {
        if (_advances.TryGetValue(glyph, out float cached)) return cached;
        float advance = MeasureAdvance(glyph);
        _advances[glyph] = advance;
        return advance;
    }

    private float MeasureAdvance(ushort glyph)
    {
        CGSize advance;
        CTFontGetAdvancesForGlyphs(_font, 0 /* horizontal */, &glyph, &advance, 1);
        return (float) advance.Width;
    }

    // How much the font's layout moves `right` when it follows `left`, beyond their own advances: Core
    // Text's layout of the pair, which applies the font's kerning, against the two advances alone.
    public float Kerning(char left, char right)
    {
        if (_kerning.TryGetValue((left, right), out float cached)) return cached;
        float kerning = MeasureKerning(left, right);
        _kerning[(left, right)] = kerning;
        return kerning;
    }

    private float MeasureKerning(char left, char right)
    {
        char* pair = stackalloc char[2] { left, right };
        IntPtr text = CFStringCreateWithCharacters(IntPtr.Zero, pair, 2);
        IntPtr key = Marshal.ReadIntPtr(NativeLibrary.GetExport(CoreText, "kCTFontAttributeName"));
        IntPtr value = _font;
        IntPtr attributes = CFDictionaryCreate(IntPtr.Zero, &key, &value, 1,
            NativeLibrary.GetExport(CoreFoundation, "kCFTypeDictionaryKeyCallBacks"),
            NativeLibrary.GetExport(CoreFoundation, "kCFTypeDictionaryValueCallBacks"));
        IntPtr attributed = CFAttributedStringCreate(IntPtr.Zero, text, attributes);
        IntPtr line = CTLineCreateWithAttributedString(attributed);
        double width = CTLineGetTypographicBounds(line, null, null, null);
        CFRelease(line);
        CFRelease(attributed);
        CFRelease(attributes);
        CFRelease(text);
        return (float) width - Advance(GlyphFor(left)) - Advance(GlyphFor(right));
    }

    // A glyph's coverage, cropped to its ink; null for a glyph with none (space).
    public GlyphInk? Rasterize(ushort glyph)
    {
        CGRect bounds;
        CTFontGetBoundingRectsForGlyphs(_font, 0, &glyph, &bounds, 1);
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        // A canvas a pixel larger than the bounds on every side, so antialiasing at the edge is not cut.
        int left = (int) Math.Floor(bounds.X) - 1, bottom = (int) Math.Floor(bounds.Y) - 1;
        int width = (int) Math.Ceiling(bounds.X + bounds.Width) + 1 - left;
        int height = (int) Math.Ceiling(bounds.Y + bounds.Height) + 1 - bottom;
        var pixels = new byte[width * height];
        fixed (byte* data = pixels)
        {
            // Alpha only: what is drawn is coverage. No font smoothing, which would thicken the stems for
            // display rather than give the outline's true coverage.
            IntPtr context = CGBitmapContextCreate(data, (nuint) width, (nuint) height, 8, (nuint) width, IntPtr.Zero, 7 /* kCGImageAlphaOnly */);
            if (context == IntPtr.Zero) throw new InvalidOperationException("Couldn't make a bitmap context");
            CGContextSetShouldAntialias(context, 1);
            CGContextSetShouldSmoothFonts(context, 0);
            CGContextSetRGBFillColor(context, 1, 1, 1, 1);
            var position = new CGPoint { X = -left, Y = -bottom };
            CTFontDrawGlyphs(_font, &glyph, &position, 1, context);
            CGContextRelease(context);
        }
        return Crop(pixels, width, height, left, bottom);
    }

    // Coverage drawn into a `width` by `height` canvas whose bottom-left sits at (`left`, `bottom`) from the
    // pen, cropped to its ink; null when nothing was drawn.
    private static GlyphInk? Crop(byte[] pixels, int width, int height, int left, int bottom)
    {
        // The first row of the bitmap is its TOP (y = bottom + height in the font's space).
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
        return new GlyphInk(iw, ih, coverage, left + x0, bottom + height - y0);
    }

    public void Dispose()
    {
        if (_font == IntPtr.Zero) return;
        CFRelease(_font);
        _font = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] private struct CGPoint { public double X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct CGSize { public double Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct CGRect { public double X, Y, Width, Height; }

    private const string CoreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreTextPath = "/System/Library/Frameworks/CoreText.framework/CoreText";
    private const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private static readonly IntPtr CoreFoundation = NativeLibrary.Load(CoreFoundationPath);
    private static readonly IntPtr CoreText = NativeLibrary.Load(CoreTextPath);

    [LibraryImport(CoreFoundationPath)] private static partial IntPtr CFURLCreateFromFileSystemRepresentation(IntPtr allocator, byte* buffer, nint length, byte isDirectory);
    [LibraryImport(CoreFoundationPath)] private static partial void CFRelease(IntPtr obj);
    [LibraryImport(CoreFoundationPath)] private static partial nint CFArrayGetCount(IntPtr array);
    [LibraryImport(CoreFoundationPath)] private static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [LibraryImport(CoreFoundationPath)] private static partial IntPtr CFStringCreateWithCharacters(IntPtr allocator, char* chars, nint length);
    [LibraryImport(CoreFoundationPath)] private static partial IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr* keys, IntPtr* values, nint count, IntPtr keyCallBacks, IntPtr valueCallBacks);
    [LibraryImport(CoreFoundationPath)] private static partial IntPtr CFAttributedStringCreate(IntPtr allocator, IntPtr text, IntPtr attributes);

    [LibraryImport(CoreTextPath)] private static partial IntPtr CTFontManagerCreateFontDescriptorsFromURL(IntPtr url);
    [LibraryImport(CoreTextPath)] private static partial IntPtr CTFontCreateWithFontDescriptor(IntPtr descriptor, double size, IntPtr matrix);
    [LibraryImport(CoreTextPath)] private static partial double CTFontGetAscent(IntPtr font);
    [LibraryImport(CoreTextPath)] private static partial double CTFontGetDescent(IntPtr font);
    [LibraryImport(CoreTextPath)] private static partial double CTFontGetLeading(IntPtr font);
    [LibraryImport(CoreTextPath)] private static partial byte CTFontGetGlyphsForCharacters(IntPtr font, char* characters, ushort* glyphs, nint count);
    [LibraryImport(CoreTextPath)] private static partial double CTFontGetAdvancesForGlyphs(IntPtr font, int orientation, ushort* glyphs, CGSize* advances, nint count);
    [LibraryImport(CoreTextPath)] private static partial CGRect CTFontGetBoundingRectsForGlyphs(IntPtr font, int orientation, ushort* glyphs, CGRect* rects, nint count);
    [LibraryImport(CoreTextPath)] private static partial void CTFontDrawGlyphs(IntPtr font, ushort* glyphs, CGPoint* positions, nint count, IntPtr context);
    [LibraryImport(CoreTextPath)] private static partial IntPtr CTLineCreateWithAttributedString(IntPtr attributed);
    [LibraryImport(CoreTextPath)] private static partial double CTLineGetTypographicBounds(IntPtr line, double* ascent, double* descent, double* leading);

    [LibraryImport(CoreGraphicsPath)] private static partial IntPtr CGBitmapContextCreate(void* data, nuint width, nuint height, nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextSetShouldAntialias(IntPtr context, byte value);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextSetShouldSmoothFonts(IntPtr context, byte value);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextSetRGBFillColor(IntPtr context, double r, double g, double b, double a);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextRelease(IntPtr context);
}

internal sealed class CoreTextFontLoader : IFontLoader
{
    public IFontFace Open(string path, float pixelSize) => new CoreTextFont(path, pixelSize);
}
