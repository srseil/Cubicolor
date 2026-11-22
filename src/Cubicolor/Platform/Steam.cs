using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cubicolor.Game;
using Cubicolor.Simulation.Levels;
using Cubicolor.Utilities;

namespace Cubicolor.Platform;

// Steamworks, through its flat C API in the library Steam ships with the SDK (libsteam_api.dylib,
// steam_api64.dll, libsteam_api.so), loaded at runtime from beside the executable. Without the library or
// without Steam running, the game runs without it: no achievements, nothing else changes.
//
// Function names are from steam_api_flat.h, checked against SDK 1.65 (July 2026) and the exports of the library
// the Steam client itself carries. An accessor's version suffix moves with its interface from SDK to SDK, so
// each is looked up by its candidate names, newest first; `--check-steam` says whether a library has them all.
internal sealed unsafe class Steam : IAchievements, IDisposable
{
    public const uint AppId = 454190;

    private readonly delegate* unmanaged<void> _runCallbacks;
    private readonly delegate* unmanaged<void> _shutdown;
    private readonly delegate* unmanaged<IntPtr, byte*, byte> _setAchievement;
    private readonly delegate* unmanaged<IntPtr, byte*, byte*, byte> _getAchievement;
    private readonly delegate* unmanaged<IntPtr, byte> _storeStats;
    private readonly IntPtr _stats;

    private Steam(IntPtr library, IntPtr stats)
    {
        _runCallbacks = (delegate* unmanaged<void>) NativeLibrary.GetExport(library, "SteamAPI_RunCallbacks");
        _shutdown = (delegate* unmanaged<void>) NativeLibrary.GetExport(library, "SteamAPI_Shutdown");
        _setAchievement = (delegate* unmanaged<IntPtr, byte*, byte>) NativeLibrary.GetExport(library, "SteamAPI_ISteamUserStats_SetAchievement");
        _getAchievement = (delegate* unmanaged<IntPtr, byte*, byte*, byte>) NativeLibrary.GetExport(library, "SteamAPI_ISteamUserStats_GetAchievement");
        _storeStats = (delegate* unmanaged<IntPtr, byte>) NativeLibrary.GetExport(library, "SteamAPI_ISteamUserStats_StoreStats");
        _stats = stats;
    }

    internal static string LibraryName =>
        OperatingSystem.IsMacOS() ? "libsteam_api.dylib" : OperatingSystem.IsWindows() ? "steam_api64.dll" : "libsteam_api.so";

    private static readonly string[] StatsAccessors = ["SteamAPI_SteamUserStats_v013", "SteamAPI_SteamUserStats_v012", "SteamAPI_SteamUserStats_v011"];
    private static readonly string[] AppsAccessors = ["SteamAPI_SteamApps_v009", "SteamAPI_SteamApps_v008"];

    // What the game calls that `library` doesn't export; each entry is one function, by its candidate names.
    internal static IReadOnlyList<string> Missing(IntPtr library) =>
    [
        .. new string[][]
        {
            ["SteamAPI_InitFlat", "SteamAPI_Init"], ["SteamAPI_RunCallbacks"], ["SteamAPI_Shutdown"],
            StatsAccessors, ["SteamAPI_ISteamUserStats_SetAchievement"], ["SteamAPI_ISteamUserStats_GetAchievement"], ["SteamAPI_ISteamUserStats_StoreStats"],
            AppsAccessors, ["SteamAPI_ISteamApps_GetCurrentGameLanguage"],
        }.Where(names => !names.Any(name => NativeLibrary.TryGetExport(library, name, out _))).Select(names => string.Join(" / ", names)),
    ];

    // Whether the Steam library beside the game loads and has everything the game calls, without starting Steam.
    public static bool Check()
    {
        string path = Path.Combine(AppContext.BaseDirectory, LibraryName);
        IntPtr library;
        try
        {
            library = NativeLibrary.Load(path);
        }
        catch (DllNotFoundException e)
        {
            Log.Error($"Steam: {path} didn't load: {e.Message}");
            return false;
        }
        IReadOnlyList<string> missing = Missing(library);
        if (missing.Count == 0) Log.Info($"Steam: {path} has everything the game calls");
        else Log.Error($"Steam: {path} lacks {string.Join(", ", missing)}");
        return missing.Count == 0;
    }

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
            foreach (string version in StatsAccessors)
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
            var steam = new Steam(library, stats) { GameLanguage = CurrentGameLanguage(library) };
            Log.Info($"Steam: running; achievements: {string.Join(", ", Enum.GetValues<Difficulty>().Select(d => $"{d} {steam.Describe(d)}"))}");
            return steam;
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

    // The language Steam runs the game in, by Steam's API name ("german", "schinese"), or null if it can't say.
    public string? GameLanguage { get; private init; }

    private static string? CurrentGameLanguage(IntPtr library)
    {
        IntPtr apps = IntPtr.Zero;
        foreach (string version in AppsAccessors)
            if (NativeLibrary.TryGetExport(library, version, out IntPtr accessor))
            {
                apps = ((delegate* unmanaged<IntPtr>) accessor)();
                break;
            }
        if (apps == IntPtr.Zero || !NativeLibrary.TryGetExport(library, "SteamAPI_ISteamApps_GetCurrentGameLanguage", out IntPtr get))
        {
            Log.Warn("Steam: no apps interface, so no game language");
            return null;
        }
        string? language = Marshal.PtrToStringUTF8(((delegate* unmanaged<IntPtr, IntPtr>) get)(apps));
        Log.Info($"Steam: game language {language}");
        return string.IsNullOrEmpty(language) ? null : language;
    }

    // Once a frame: lets Steam deliver its callbacks.
    public void Tick() => _runCallbacks();

    // COMPLETE_NORMAL, COMPLETE_SMART and COMPLETE_GENIUS, as both earlier versions named them on Steam.
    public void Unlock(Difficulty finished)
    {
        // Already the player's: nothing to tell Steam.
        if (IsHeld(finished) == true) return;
        fixed (byte* name = Name(finished))
            if (_setAchievement(_stats, name) == 0)
            {
                Log.Warn($"Steam: couldn't set the achievement for {finished}");
                return;
            }
        _storeStats(_stats);
        Log.Info($"Steam: achievement for {finished}");
    }

    // Whether the player has a difficulty's achievement, or null when Steam doesn't know of it.
    private bool? IsHeld(Difficulty difficulty)
    {
        byte held = 0;
        fixed (byte* name = Name(difficulty)) return _getAchievement(_stats, name, &held) != 0 ? held != 0 : null;
    }

    private string Describe(Difficulty difficulty) => IsHeld(difficulty) switch { true => "held", false => "not held", null => "unknown" };

    private static byte[] Name(Difficulty difficulty) => System.Text.Encoding.ASCII.GetBytes($"COMPLETE_{difficulty.ToString().ToUpperInvariant()}\0");

    public void Dispose() => _shutdown();
}
