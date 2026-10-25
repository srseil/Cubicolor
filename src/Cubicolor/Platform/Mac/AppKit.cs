using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cubicolor.Utilities;
using static Cubicolor.Platform.Mac.ObjC;

namespace Cubicolor.Platform.Mac;

// The game as a macOS application (docs/native-macos.md §4.3): NSApplication, its menu, and the event pump
// the frame loop runs once a frame. Windowed runs only; a headless run never touches AppKit, so it never
// shows up in the Dock.
//
// The game owns its loop, so the application is never `run`: each frame takes every queued event, lets the
// input see it (MacInput), then hands it on to AppKit with sendEvent:, which delivers it to the window and
// view as usual and keeps menus, window dragging and text input working.
internal static unsafe class AppKit
{
    private const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private static readonly IntPtr Library = NativeLibrary.Load(AppKitPath);

    public static IntPtr App { get; private set; }

    // Set when the user quits through the menu, Cmd-Q or the Dock: the frame loop ends the run itself, so
    // the application is never terminated out from under it.
    public static bool QuitRequested { get; private set; }

    private static IntPtr _delegate;

    public static void Start(string title)
    {
        NativeLibrary.Load(AppKitPath);   // before the first class lookup: the field above loads it only when first read
        using var pool = AutoreleasePool.Push();
        App = Send<IntPtr>(Class("NSApplication"), Sel("sharedApplication"));

        IntPtr delegateClass = DefineClass("CubicolorAppDelegate", "NSObject", new[]
        {
            ("applicationShouldTerminate:", (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, nuint>) &ShouldTerminate, "Q@:@"),
        }, "NSApplicationDelegate");
        _delegate = New(delegateClass);
        Call(App, Sel("setDelegate:"), _delegate);
        Call(App, Sel("setActivationPolicy:"), (nint) 0);   // NSApplicationActivationPolicyRegular

        // The menu bar's application menu, with Quit on Cmd-Q. It sends terminate:, which asks the delegate
        // above, which ends the run through the loop.
        IntPtr menuBar = New(Class("NSMenu"));
        IntPtr appItem = New(Class("NSMenuItem"));
        Call(menuBar, Sel("addItem:"), appItem);
        IntPtr appMenu = New(Class("NSMenu"));
        IntPtr quit = Send<IntPtr, IntPtr, IntPtr, IntPtr>(Send<IntPtr>(Class("NSMenuItem"), Sel("alloc")),
            Sel("initWithTitle:action:keyEquivalent:"), NSString($"Quit {title}"), Sel("terminate:"), NSString("q"));
        Call(appMenu, Sel("addItem:"), quit);
        Call(appItem, Sel("setSubmenu:"), appMenu);
        Call(App, Sel("setMainMenu:"), menuBar);

        Call(App, Sel("finishLaunching"));
        Activate();
    }

    // Bring the game to the front, once its window exists too (Window calls it). macOS 14 asks apps to
    // activate cooperatively (`activate`); the older call is the fallback.
    public static void Activate()
    {
        if (App == IntPtr.Zero) return;
        if (Send<byte, IntPtr>(App, Sel("respondsToSelector:"), Sel("activate")) != 0) Call(App, Sel("activate"));
        else Call(App, Sel("activateIgnoringOtherApps:"), (byte) 1);
    }

    [UnmanagedCallersOnly]
    private static nuint ShouldTerminate(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        QuitRequested = true;
        return 0;   // NSTerminateCancel: the loop quits
    }

    private static readonly IntPtr SelNextEvent = Sel("nextEventMatchingMask:untilDate:inMode:dequeue:"),
        SelSendEvent = Sel("sendEvent:"), SelUpdateWindows = Sel("updateWindows");

    // Take every queued event: `observe` sees each first, then AppKit delivers it.
    public static void PumpEvents(Action<IntPtr> observe)
    {
        if (App == IntPtr.Zero) return;
        using var pool = AutoreleasePool.Push();
        IntPtr past = Send<IntPtr>(Class("NSDate"), Sel("distantPast"));
        IntPtr mode = NSString("kCFRunLoopDefaultMode");
        while (true)
        {
            IntPtr e = Send<IntPtr, ulong, IntPtr, IntPtr, byte>(App, SelNextEvent, ulong.MaxValue, past, mode, 1);
            if (e == IntPtr.Zero) break;
            observe(e);
            Call(App, SelSendEvent, e);
        }
        Call(App, SelUpdateWindows);
    }

    // Sleep until an event arrives or `seconds` pass, leaving the event queued for PumpEvents.
    public static void WaitForEvent(double seconds)
    {
        if (App == IntPtr.Zero) return;
        using var pool = AutoreleasePool.Push();
        IntPtr until = Send<IntPtr, double>(Class("NSDate"), Sel("dateWithTimeIntervalSinceNow:"), seconds);
        Send<IntPtr, ulong, IntPtr, IntPtr, byte>(App, SelNextEvent, ulong.MaxValue, until, NSString("kCFRunLoopDefaultMode"), 0);
    }

    // The languages the user prefers, most preferred first, as tags ("de-DE", "zh-Hans-CN").
    public static string[] PreferredLanguages()
    {
        using var pool = AutoreleasePool.Push();
        IntPtr array = Send<IntPtr>(Class("NSLocale"), Sel("preferredLanguages"));
        int count = (int) Send<nuint>(array, Sel("count"));
        var tags = new string[count];
        for (int i = 0; i < count; i++)
            tags[i] = Marshal.PtrToStringUTF8(Send<IntPtr>(Send<IntPtr, nuint>(array, Sel("objectAtIndex:"), (nuint) i), Sel("UTF8String"))) ?? "";
        return tags;
    }

    // A web page in the default browser.
    public static void OpenUrl(string url)
    {
        using var pool = AutoreleasePool.Push();
        IntPtr nsurl = Send<IntPtr, IntPtr>(Class("NSURL"), Sel("URLWithString:"), NSString(url));
        if (nsurl == IntPtr.Zero) return;
        Send<byte, IntPtr>(Send<IntPtr>(Class("NSWorkspace"), Sel("sharedWorkspace")), Sel("openURL:"), nsurl);
    }

    // The application's icon, which the Dock shows while the game runs, from an image file. A missing or
    // unreadable file is a warning, not a reason not to start.
    public static void SetIcon(string path)
    {
        using var pool = AutoreleasePool.Push();
        IntPtr image = Send<IntPtr, IntPtr>(Send<IntPtr>(Class("NSImage"), Sel("alloc")), Sel("initWithContentsOfFile:"), NSString(path));
        if (image == IntPtr.Zero)
        {
            Log.Warn($"Couldn't load the app icon '{path}'");
            return;
        }
        Call(App, Sel("setApplicationIconImage:"), image);
        objc_release(image);
        Log.Info($"App icon set from {path}");
    }
}
