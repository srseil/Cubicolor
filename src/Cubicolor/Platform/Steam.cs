using System;
using System.IO;
using System.Runtime.InteropServices;
using Cubicolor.Game;
using Cubicolor.Simulation.Levels;
using Cubicolor.Utilities;

namespace Cubicolor.Platform;

// Steamworks, through its flat C API in the library Steam ships with the SDK (libsteam_api.dylib,
// steam_api64.dll, libsteam_api.so), loaded at runtime from beside the executable. Without the library or
// without Steam running, the game runs without it: no achievements, nothing else changes.
//
// Function names are from steam_api_flat.h. Newer SDKs (1.58 on) export SteamAPI_InitFlat and drop the
// plain SteamAPI_Init export, and the accessor's version suffix moves with the interface, so each is looked
// up by its candidate names (from memory; to check against the SDK that ships).
internal sealed unsafe class Steam : IAchievements, IDisposable
{
    public const uint AppId = 454190;

    private readonly delegate* unmanaged<void> _runCallbacks;
    private readonly delegate* unmanaged<void> _shutdown;
    private readonly delegate* unmanaged<IntPtr, byte*, byte> _setAchievement;
    private readonly delegate* unmanaged<IntPtr, byte> _storeStats;
    private readonly IntPtr _stats;

    private Steam(IntPtr library, IntPtr stats)
    {
        _runCallbacks = (delegate* unmanaged<void>) NativeLibrary.GetExport(library, "SteamAPI_RunCallbacks");
        _shutdown = (delegate* unmanaged<void>) NativeLibrary.GetExport(library, "SteamAPI_Shutdown");
        _setAchievement = (delegate* unmanaged<IntPtr, byte*, byte>) NativeLibrary.GetExport(library, "SteamAPI_ISteamUserStats_SetAchievement");
        _storeStats = (delegate* unmanaged<IntPtr, byte>) NativeLibrary.GetExport(library, "SteamAPI_ISteamUserStats_StoreStats");
        _stats = stats;
    }

    private static string LibraryName =>
        OperatingSystem.IsMacOS() ? "libsteam_api.dylib" : OperatingSystem.IsWindows() ? "steam_api64.dll" : "libsteam_api.so";

    // Steam, or null when it is not there to talk to.
    public static Steam? Start()
    {
        string path = Path.Combine(AppContext.BaseDirectory, LibraryName);
        if (!NativeLibrary.TryLoad(path, out IntPtr library))
        {
            Log.Info($"Steam: no {LibraryName} beside the game, running without it");
            return null;
        }
        try
        {
            if (!Initialise(library)) return null;
            IntPtr stats = IntPtr.Zero;
            foreach (string version in new[] { "SteamAPI_SteamUserStats_v013", "SteamAPI_SteamUserStats_v012", "SteamAPI_SteamUserStats_v011" })
                if (NativeLibrary.TryGetExport(library, version, out IntPtr accessor))
                {
                    stats = ((delegate* unmanaged<IntPtr>) accessor)();
                    break;
                }
            if (stats == IntPtr.Zero)
            {
                Log.Warn("Steam: no user stats interface");
                return null;
            }
            // Older SDKs want the stats asked for before achievements can be set; newer ones fetch them on their own.
            if (NativeLibrary.TryGetExport(library, "SteamAPI_ISteamUserStats_RequestCurrentStats", out IntPtr request))
                ((delegate* unmanaged<IntPtr, byte>) request)(stats);
            Log.Info("Steam: running");
            return new Steam(library, stats);
        }
        catch (EntryPointNotFoundException e)
        {
            Log.Warn($"Steam: {e.Message}");
            return null;
        }
    }

    private static bool Initialise(IntPtr library)
    {
        if (NativeLibrary.TryGetExport(library, "SteamAPI_InitFlat", out IntPtr initFlat))
        {
            // ESteamAPIInitResult: 0 is OK; the error text goes into a 1024-byte buffer.
            byte* error = stackalloc byte[1024];
            int result = ((delegate* unmanaged<byte*, int>) initFlat)(error);
            if (result == 0) return true;
            Log.Info($"Steam: not started ({result}: {Marshal.PtrToStringUTF8((IntPtr) error)})");
            return false;
        }
        if (NativeLibrary.TryGetExport(library, "SteamAPI_Init", out IntPtr init))
        {
            if (((delegate* unmanaged<byte>) init)() != 0) return true;
            Log.Info("Steam: not started (is Steam running?)");
            return false;
        }
        Log.Warn("Steam: the library has no init function");
        return false;
    }

    // Once a frame: lets Steam deliver its callbacks.
    public void Tick() => _runCallbacks();

    // COMPLETE_NORMAL, COMPLETE_SMART and COMPLETE_GENIUS, as both earlier versions named them on Steam.
    public void Unlock(Difficulty finished)
    {
        string name = $"COMPLETE_{finished.ToString().ToUpperInvariant()}\0";
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(name);
        fixed (byte* p = bytes)
            if (_setAchievement(_stats, p) == 0) Log.Warn($"Steam: couldn't set {name.TrimEnd('\0')}");
        _storeStats(_stats);
        Log.Info($"Steam: achievement {name.TrimEnd('\0')}");
    }

    public void Dispose() => _shutdown();
}
