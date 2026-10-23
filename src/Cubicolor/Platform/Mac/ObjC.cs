using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Cubicolor.Platform.Mac;

// The Objective-C runtime, called directly: classes and selectors by name, and objc_msgSend cast to the
// exact signature of each call. On arm64 there is one objc_msgSend for every signature (no _stret or
// _fpret variants), and a C# function pointer of the right type gets the calling convention right,
// structs passed by value included. No bindings package, no native shim (docs/metal-renderer.md §3). Used
// by the renderer (Core/Metal) and the platform layer (AppKit, Window, MacInput), which also defines a few
// classes of its own through it (DefineClass).
//
// Selectors are resolved once, into static fields of the wrappers (MetalTypes.cs, MacInput), never per call:
// looking one up by name cost about as much as the call itself in the spike.
//
// Ownership follows Cocoa's rules: what a `new…`, `alloc`/`init` or `copy` call returns is ours to
// Release; everything else is autoreleased and lives until the enclosing AutoreleasePool is popped.
internal static unsafe partial class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    private static readonly IntPtr MsgSend;

    static ObjC()
    {
        MsgSend = NativeLibrary.GetExport(NativeLibrary.Load(Lib), "objc_msgSend");
        // NSString and friends have to be loaded before objc_getClass can find them.
        NativeLibrary.Load("/System/Library/Frameworks/Foundation.framework/Foundation");
    }

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial IntPtr objc_getClass(string name);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial IntPtr sel_registerName(string name);
    [LibraryImport(Lib)] public static partial IntPtr objc_autoreleasePoolPush();
    [LibraryImport(Lib)] public static partial void objc_autoreleasePoolPop(IntPtr pool);
    [LibraryImport(Lib)] public static partial void objc_release(IntPtr obj);
    [LibraryImport(Lib)] public static partial IntPtr objc_retain(IntPtr obj);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial IntPtr objc_allocateClassPair(IntPtr superclass, string name, nuint extraBytes);
    [LibraryImport(Lib)] private static partial void objc_registerClassPair(IntPtr cls);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial byte class_addMethod(IntPtr cls, IntPtr sel, IntPtr imp, string types);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial IntPtr objc_getProtocol(string name);
    [LibraryImport(Lib)] private static partial byte class_addProtocol(IntPtr cls, IntPtr protocol);

    public static IntPtr Sel(string name) => sel_registerName(name);

    // A new Objective-C class, a subclass of `superclass`, with `methods` implemented by C# functions
    // ([UnmanagedCallersOnly], taking self and _cmd first) and conforming to `protocols`. For the few places
    // AppKit wants an object of ours: the game's view, the window and application delegates. Each is defined
    // once per process; the name must be unique. `types` is each method's Objective-C type encoding.
    public static IntPtr DefineClass(string name, string superclass, IEnumerable<(string Selector, IntPtr Function, string Types)> methods,
                                     params string[] protocols)
    {
        IntPtr cls = objc_allocateClassPair(Class(superclass), name, 0);
        if (cls == IntPtr.Zero) throw new InvalidOperationException($"Couldn't define the Objective-C class {name} (taken?)");
        foreach ((string selector, IntPtr function, string types) in methods)
            if (class_addMethod(cls, Sel(selector), function, types) == 0)
                throw new InvalidOperationException($"Couldn't add {selector} to {name}");
        foreach (string protocol in protocols)
            if (objc_getProtocol(protocol) is var p && p != IntPtr.Zero) class_addProtocol(cls, p);
        objc_registerClassPair(cls);
        return cls;
    }

    public static IntPtr Class(string name)
    {
        IntPtr cls = objc_getClass(name);
        if (cls == IntPtr.Zero) throw new InvalidOperationException($"no Objective-C class {name}");
        return cls;
    }

    // Typed message sends: R the return type, A.. the arguments, all blittable.
    public static R Send<R>(IntPtr obj, IntPtr sel) where R : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, R>) MsgSend)(obj, sel);
    public static R Send<R, A>(IntPtr obj, IntPtr sel, A a) where R : unmanaged where A : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, R>) MsgSend)(obj, sel, a);
    public static R Send<R, A, B>(IntPtr obj, IntPtr sel, A a, B b) where R : unmanaged where A : unmanaged where B : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, R>) MsgSend)(obj, sel, a, b);
    public static R Send<R, A, B, C>(IntPtr obj, IntPtr sel, A a, B b, C c) where R : unmanaged where A : unmanaged where B : unmanaged where C : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, C, R>) MsgSend)(obj, sel, a, b, c);
    public static R Send<R, A, B, C, D>(IntPtr obj, IntPtr sel, A a, B b, C c, D d) where R : unmanaged where A : unmanaged where B : unmanaged where C : unmanaged where D : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, C, D, R>) MsgSend)(obj, sel, a, b, c, d);

    public static void Call(IntPtr obj, IntPtr sel)
        => ((delegate* unmanaged<IntPtr, IntPtr, void>) MsgSend)(obj, sel);
    public static void Call<A>(IntPtr obj, IntPtr sel, A a) where A : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, void>) MsgSend)(obj, sel, a);
    public static void Call<A, B>(IntPtr obj, IntPtr sel, A a, B b) where A : unmanaged where B : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, void>) MsgSend)(obj, sel, a, b);
    public static void Call<A, B, C>(IntPtr obj, IntPtr sel, A a, B b, C c) where A : unmanaged where B : unmanaged where C : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, C, void>) MsgSend)(obj, sel, a, b, c);
    public static void Call<A, B, C, D>(IntPtr obj, IntPtr sel, A a, B b, C c, D d) where A : unmanaged where B : unmanaged where C : unmanaged where D : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, C, D, void>) MsgSend)(obj, sel, a, b, c, d);
    public static void Call<A, B, C, D, E>(IntPtr obj, IntPtr sel, A a, B b, C c, D d, E e)
        where A : unmanaged where B : unmanaged where C : unmanaged where D : unmanaged where E : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, C, D, E, void>) MsgSend)(obj, sel, a, b, c, d, e);
    public static void Call<A, B, C, D, E, F, G, H, I>(IntPtr obj, IntPtr sel, A a, B b, C c, D d, E e, F f, G g, H h, I i)
        where A : unmanaged where B : unmanaged where C : unmanaged where D : unmanaged where E : unmanaged
        where F : unmanaged where G : unmanaged where H : unmanaged where I : unmanaged
        => ((delegate* unmanaged<IntPtr, IntPtr, A, B, C, D, E, F, G, H, I, void>) MsgSend)(obj, sel, a, b, c, d, e, f, g, h, i);

    private static readonly IntPtr SelAlloc = Sel("alloc"), SelInit = Sel("init");
    private static readonly IntPtr SelStringWithUtf8 = Sel("stringWithUTF8String:"), SelUtf8String = Sel("UTF8String");
    private static readonly IntPtr SelLocalizedDescription = Sel("localizedDescription");
    private static readonly IntPtr NSStringClass = Class("NSString");

    // alloc + init: an owned object, to Release.
    public static IntPtr New(IntPtr cls) => Send<IntPtr>(Send<IntPtr>(cls, SelAlloc), SelInit);

    // An autoreleased NSString.
    public static IntPtr NSString(string s)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(s + "\0");
        fixed (byte* p = utf8) return Send<IntPtr, IntPtr>(NSStringClass, SelStringWithUtf8, (IntPtr) p);
    }

    public static string? Str(IntPtr nsString)
        => nsString == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send<IntPtr>(nsString, SelUtf8String));

    public static string ErrorText(IntPtr nsError)
        => nsError == IntPtr.Zero ? "(no error given)" : Str(Send<IntPtr>(nsError, SelLocalizedDescription)) ?? "(no description)";

    public static void Release(ref IntPtr obj)
    {
        if (obj != IntPtr.Zero) objc_release(obj);
        obj = IntPtr.Zero;
    }
}

// A scope whose autoreleased objects are freed at its end: one per frame, and one around setup work that
// creates temporaries (descriptors, strings).
internal readonly struct AutoreleasePool : IDisposable
{
    private readonly IntPtr _pool;
    private AutoreleasePool(IntPtr pool) => _pool = pool;
    public static AutoreleasePool Push() => new(ObjC.objc_autoreleasePoolPush());
    public void Dispose() => ObjC.objc_autoreleasePoolPop(_pool);
}
