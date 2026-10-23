using System;
using System.Runtime.InteropServices;

namespace Cubicolor.Platform.Mac;

// Core Graphics' geometry, as AppKit takes and returns it (NSPoint, NSSize and NSRect are the same structs on
// 64-bit macOS). Doubles, origin at the bottom left in AppKit's coordinates.
[StructLayout(LayoutKind.Sequential)] internal struct CGPoint { public double X, Y; }
[StructLayout(LayoutKind.Sequential)] internal struct CGSize { public double Width, Height; }
[StructLayout(LayoutKind.Sequential)] internal struct CGRect
{
    public double X, Y, Width, Height;
    public CGRect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
}

// The few Core Foundation calls the platform layer needs: releasing what a Create or Copy call returned,
// reading arrays and data, and a one-entry dictionary for option arguments.
internal static unsafe partial class CoreFoundation
{
    public const string Path = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public static readonly IntPtr Library = NativeLibrary.Load(Path);

    [LibraryImport(Path)] public static partial void CFRelease(IntPtr obj);
    [LibraryImport(Path)] public static partial nint CFArrayGetCount(IntPtr array);
    [LibraryImport(Path)] public static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [LibraryImport(Path)] public static partial byte* CFDataGetBytePtr(IntPtr data);
    [LibraryImport(Path)] private static partial IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr* keys, IntPtr* values, nint count, IntPtr keyCallBacks, IntPtr valueCallBacks);

    // The value of an exported constant: a CFStringRef or CFBooleanRef global, read through its symbol.
    public static IntPtr Constant(IntPtr library, string symbol) => Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));

    public static IntPtr True => Constant(Library, "kCFBooleanTrue");

    // A dictionary of one key and value, both CF objects; owned, to CFRelease.
    public static IntPtr Dictionary(IntPtr key, IntPtr value)
        => CFDictionaryCreate(IntPtr.Zero, &key, &value, 1,
            NativeLibrary.GetExport(Library, "kCFTypeDictionaryKeyCallBacks"),
            NativeLibrary.GetExport(Library, "kCFTypeDictionaryValueCallBacks"));
}
