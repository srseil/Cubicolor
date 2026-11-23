using System;

namespace Cubicolor.Platform.Windows;

// COM without the runtime's COM interop (which NativeAOT doesn't have): an interface pointer points at its
// vtable, and a method is called through its slot, numbered as in the SDK header with IUnknown's three first.
// Each wrapper class names the slots it uses beside the call.
internal static unsafe class Com
{
    public static void* Slot(IntPtr instance, int index) => (*(void***) instance)[index];

    public static IntPtr Query(IntPtr instance, Guid iid)
    {
        IntPtr result;
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>) Slot(instance, 0))(instance, &iid, &result);
        return hr < 0 ? IntPtr.Zero : result;
    }

    public static void Release(ref IntPtr instance)
    {
        if (instance == IntPtr.Zero) return;
        ((delegate* unmanaged[Stdcall]<IntPtr, uint>) Slot(instance, 2))(instance);
        instance = IntPtr.Zero;
    }

    public static void Check(int hr, string what)
    {
        if (hr < 0) throw new InvalidOperationException($"{what} failed (HRESULT 0x{hr:X8})");
    }
}
