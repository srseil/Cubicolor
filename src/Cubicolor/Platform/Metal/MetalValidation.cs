using System;
using System.Runtime.InteropServices;

namespace Cubicolor.Platform.Metal;

// Metal's API validation, for checked runs (AppLoop.IsChecked). SDL_GPU's device ran in debug mode for every
// run, so misuse failed the run (import-plan A9); Metal checks nothing unless MTL_DEBUG_LAYER is set in the
// process's environment before the first device is made. A validation error then stops the process with
// Metal's message on stderr.
//
// Set with libc's setenv, since .NET's Environment.SetEnvironmentVariable changes only the runtime's own copy
// on Unix, which Metal never reads. Without overwriting: MTL_DEBUG_LAYER=0 in the caller's environment
// keeps it off, to time a checked run without the layer's CPU cost.
internal static partial class MetalValidation
{
    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int setenv(string name, string value, int overwrite);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr getenv(string name);

    public static void Enable() => setenv("MTL_DEBUG_LAYER", "1", 0);

    // Whether the process's environment asks for the layer.
    public static bool Requested => Marshal.PtrToStringUTF8(getenv("MTL_DEBUG_LAYER")) is "1";
}
