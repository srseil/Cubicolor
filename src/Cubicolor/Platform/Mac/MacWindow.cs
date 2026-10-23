using System;
using System.Runtime.InteropServices;
using Cubicolor.Utilities;
using static Cubicolor.Platform.Mac.ObjC;

namespace Cubicolor.Platform.Mac;

// The game's window: an NSWindow whose content is a view of our own hosting the CAMetalLayer the renderer
// draws into. Resizable, and fullscreen is the system's own (its own Space). Sizes are in points; the layer
// is kept at the display's backing scale, so drawing happens at full pixel resolution.
internal sealed unsafe class MacWindow : IWindow
{
    internal IntPtr Handle { get; }
    internal IntPtr View { get; }
    internal IntPtr MetalLayer { get; }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool CloseRequested { get; private set; }

    private static MacWindow? _current;   // the one window, for the delegate's methods
    private static IntPtr _viewClass, _delegateClass;
    private readonly IntPtr _delegate;

    private const nuint Titled = 1, Closable = 2, Miniaturizable = 4, Resizable = 8;

    public MacWindow(int width, int height, string title)
    {
        _current = this;
        using var pool = AutoreleasePool.Push();
        DefineClasses();

        var rect = new CGRect(0, 0, width, height);
        Handle = Send<IntPtr, CGRect, nuint, nuint, byte>(Send<IntPtr>(Class("NSWindow"), Sel("alloc")),
            Sel("initWithContentRect:styleMask:backing:defer:"), rect, Titled | Closable | Miniaturizable | Resizable, 2 /* buffered */, 0);
        Call(Handle, Sel("setReleasedWhenClosed:"), (byte) 0);
        Call(Handle, Sel("setTitle:"), NSString(title));
        Call(Handle, Sel("setCollectionBehavior:"), (nuint) (1 << 7));   // NSWindowCollectionBehaviorFullScreenPrimary
        Call(Handle, Sel("setAcceptsMouseMovedEvents:"), (byte) 1);
        Call(Handle, Sel("setContentMinSize:"), new CGSize { Width = 480, Height = 360 });
        _delegate = New(_delegateClass);
        Call(Handle, Sel("setDelegate:"), _delegate);

        // Layer-hosting: the view's layer IS the Metal layer.
        View = Send<IntPtr, CGRect>(Send<IntPtr>(_viewClass, Sel("alloc")), Sel("initWithFrame:"), rect);
        MetalLayer = objc_retain(Send<IntPtr>(Class("CAMetalLayer"), Sel("layer")));
        Call(View, Sel("setLayer:"), MetalLayer);
        Call(View, Sel("setWantsLayer:"), (byte) 1);
        Call(Handle, Sel("setContentView:"), View);
        Call(Handle, Sel("makeFirstResponder:"), View);
        Call(Handle, Sel("center"));
        Call(Handle, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
        AppKit.Activate();

        RefreshSize();
        Log.Info($"Window created: {Width}x{Height} points");
    }

    // Pixels per point: 2 on Retina displays.
    public float PixelDensity => (float) Send<double>(Handle, Sel("backingScaleFactor"));

    public bool IsFocused => Send<byte>(Handle, Sel("isKeyWindow")) != 0;
    public bool IsFullscreen => (Send<nuint>(Handle, Sel("styleMask")) & (1u << 14)) != 0;

    // The pointer over the window, in points from its top left (it may lie outside).
    internal (float X, float Y) MouseLocation()
    {
        var p = Send<CGPoint>(Handle, Sel("mouseLocationOutsideOfEventStream"));
        return ((float) p.X, (float) (Height - p.Y));
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen != IsFullscreen) Call(Handle, Sel("toggleFullScreen:"), IntPtr.Zero);
    }

    // Re-read the size from the view, and keep the layer's scale at the display's. Called every frame.
    public void RefreshSize()
    {
        var bounds = Send<CGRect>(View, Sel("bounds"));
        Call(MetalLayer, Sel("setContentsScale:"), (double) PixelDensity);
        Width = Math.Max(1, (int) Math.Round(bounds.Width));
        Height = Math.Max(1, (int) Math.Round(bounds.Height));
    }

    public void RequestClose() => CloseRequested = true;

    public void Dispose()
    {
        Call(Handle, Sel("setDelegate:"), IntPtr.Zero);
        Call(Handle, Sel("close"));
        IntPtr handle = Handle, view = View, layer = MetalLayer, windowDelegate = _delegate;
        Release(ref view);
        Release(ref layer);
        Release(ref handle);
        Release(ref windowDelegate);
        _current = null;
    }

    private static void DefineClasses()
    {
        if (_viewClass != IntPtr.Zero) return;
        _viewClass = DefineClass("CubicolorView", "NSView", new (string, IntPtr, string)[]
        {
            ("acceptsFirstResponder", (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, byte>) &Yes, "c@:"),
            // Keys are read in the event pump (MacInput); taking them here keeps AppKit from beeping.
            ("keyDown:", (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>) &Ignore, "v@:@"),
        });
        _delegateClass = DefineClass("CubicolorWindowDelegate", "NSObject", new (string, IntPtr, string)[]
        {
            ("windowShouldClose:", (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>) &ShouldClose, "c@:@"),
        }, "NSWindowDelegate");
    }

    [UnmanagedCallersOnly] private static byte Yes(IntPtr self, IntPtr cmd) => 1;

    [UnmanagedCallersOnly] private static void Ignore(IntPtr self, IntPtr cmd, IntPtr e) { }

    [UnmanagedCallersOnly]
    private static byte ShouldClose(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        _current?.RequestClose();
        return 0;   // the loop ends the run and disposes the window
    }
}
