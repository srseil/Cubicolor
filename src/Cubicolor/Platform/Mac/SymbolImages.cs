using System;
using System.Runtime.InteropServices;
using Cubicolor.Render;
using static Cubicolor.Platform.Mac.ObjC;

namespace Cubicolor.Platform.Mac;

// SF Symbols, drawn by AppKit into coverage the way CoreTextFont draws a glyph, so the controls screen can show
// a controller's buttons as the controller names them. A symbol is a template image: its alpha is its shape.
internal static unsafe partial class SymbolImages
{
    private const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // The symbol `name` at `pixels` high (one pixel a point), cropped to its ink; null for a name this macOS
    // does not have.
    public static GlyphInk? Rasterize(string name, float pixels)
    {
        NativeLibrary.Load(AppKitPath);
        using var pool = AutoreleasePool.Push();
        IntPtr image = Send<IntPtr, IntPtr, IntPtr>(Class("NSImage"), Sel("imageWithSystemSymbolName:accessibilityDescription:"), NSString(name), IntPtr.Zero);
        if (image == IntPtr.Zero) return null;
        IntPtr config = Send<IntPtr, double, double>(Class("NSImageSymbolConfiguration"), Sel("configurationWithPointSize:weight:"), pixels, 0.0);
        image = Send<IntPtr, IntPtr>(image, Sel("imageWithSymbolConfiguration:"), config);
        CGSize size = Send<CGSize>(image, Sel("size"));
        IntPtr cgImage = Send<IntPtr, IntPtr, IntPtr, IntPtr>(image, Sel("CGImageForProposedRect:context:hints:"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (cgImage == IntPtr.Zero || size.Width <= 0 || size.Height <= 0) return null;

        int width = (int) Math.Ceiling(size.Width) + 2, height = (int) Math.Ceiling(size.Height) + 2;
        var rgba = new byte[width * height * 4];
        fixed (byte* data = rgba)
        {
            // RGBA, of which only the alpha is kept: an alpha-only context drew the strokes hollow.
            IntPtr space = CGColorSpaceCreateDeviceRGB();
            IntPtr context = CGBitmapContextCreate(data, (nuint) width, (nuint) height, 8, (nuint) width * 4, space, 1 /* premultiplied last */);
            CGColorSpaceRelease(space);
            if (context == IntPtr.Zero) return null;
            CGContextSetInterpolationQuality(context, 3);
            CGContextDrawImage(context, new CGRect(1, 1, size.Width, size.Height), cgImage);
            CGContextRelease(context);
        }
        var coverage = new byte[width * height];
        for (int i = 0; i < coverage.Length; i++) coverage[i] = rgba[i * 4 + 3];
        return new GlyphInk(width, height, coverage, 0, height);
    }

    [LibraryImport(CoreGraphicsPath)] private static partial IntPtr CGBitmapContextCreate(void* data, nuint width, nuint height, nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextRelease(IntPtr context);
    [LibraryImport(CoreGraphicsPath)] private static partial IntPtr CGColorSpaceCreateDeviceRGB();
    [LibraryImport(CoreGraphicsPath)] private static partial void CGColorSpaceRelease(IntPtr space);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextSetInterpolationQuality(IntPtr context, int quality);
    [LibraryImport(CoreGraphicsPath)] private static partial void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);
}
