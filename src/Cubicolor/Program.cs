using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cubicolor.App;
using Cubicolor.Audio;
using Cubicolor.Game;
using Cubicolor.Platform;
using Cubicolor.Platform.Mac;
using Cubicolor.Platform.Metal;
using Cubicolor.Render;
using Cubicolor.Simulation.Levels;
using Cubicolor.Simulation.Saves;
using Cubicolor.Utilities;

namespace Cubicolor;

// The composition root: options, the platform, the services, the loop.
internal static class Program
{
    private const string Usage = """
        Cubicolor [options]
          --headless              no window: render offscreen at a fixed 60 frames a second
          --script <file>         run a script of commands (see App/Script.cs), headless; the run ends with it
          --exec "<commands>"     the same, inline, ';'-separated
          --size <W>x<H>          headless frame size in pixels (default 1280x720)
          --level <name>          start in a level, e.g. normal03
          --language <code>       show the game in a language (en, fr, de, es, zh-Hans, ru, pt-BR, ja, ko, pl);
                                  headless runs are in English otherwise
          --data-dir <dir>        where progress and settings live (headless default: a fresh temporary one)
          --timeout <seconds>     fail a run that takes longer
          --capture-audio <wav>   headless: write what the game played to a WAV file
          --record <file>         headless: write every frame's pixels to a file, raw RGBA (a named pipe
                                  into ffmpeg makes a video of it)
          --art <dir>             render the Steam store and library images, the icon and screenshots into <dir>
          --perf                  headless: report frame times (CPU, GPU), draw calls and allocations at the end;
                                  in a window: log frames drawn, still and hidden and the CPU used, every 5 s
        """;

    private static int Main(string[] args)
    {
        bool headless = false;
        string? script = null, level = null, dataDir = null, captureAudio = null, record = null, art = null;
        bool perf = false;
        Language? language = null;
        int width = 1280, height = 720;
        double timeout = double.PositiveInfinity;
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--headless": headless = true; break;
                    case "--script": script = File.ReadAllText(args[++i]); break;
                    case "--exec": script = args[++i]; break;
                    case "--level": level = args[++i]; break;
                    case "--language":
                        language = Languages.FromCode(args[++i]) ?? throw new ArgumentException($"No language {args[i]}");
                        break;
                    case "--data-dir": dataDir = args[++i]; break;
                    case "--capture-audio": captureAudio = args[++i]; break;
                    case "--record": record = args[++i]; break;
                    case "--art": art = args[++i]; headless = true; break;
                    case "--perf": perf = true; break;
                    case "--timeout": timeout = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--size":
                        string[] parts = args[++i].Split('x');
                        (width, height) = (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
                        break;
                    case "--help": Console.WriteLine(Usage); return 0;
                    default: throw new ArgumentException($"Unknown option {args[i]}");
                }
            }
        }
        catch (Exception e) when (e is ArgumentException or FormatException or IndexOutOfRangeException or IOException)
        {
            Console.Error.WriteLine($"{e.Message}\n{Usage}");
            return 2;
        }

        if (script != null && !headless)
        {
            Console.Error.WriteLine("Scripts run headless only: add --headless");
            return 2;
        }

        string assets = AssetsDirectory();
        bool temporary = headless && dataDir == null;
        if (temporary) dataDir = Path.Combine(Path.GetTempPath(), "cubicolor-" + Guid.NewGuid().ToString("N"));
        SaveLocations locations = dataDir != null
            ? new SaveLocations(dataDir, [], [])
            : SaveLocations.For(SaveLocations.Platform.MacOS, Environment.GetEnvironmentVariable, InstallDirectories());
        SaveStore saves = SaveStore.Load(locations);
        foreach (string source in saves.Sources) Log.Info($"Loaded {source}");

        try
        {
            if (art != null) return RenderArt(saves, assets, art, language);
            return headless
                ? RunHeadless(saves, assets, width, height, script, level, timeout, captureAudio, record, perf, language)
                : RunWindowed(saves, assets, level, perf, language);
        }
        finally
        {
            if (temporary && Directory.Exists(dataDir)) Directory.Delete(dataDir!, recursive: true);
        }
    }

    // The game's data: beside the executable, or in a macOS bundle's Contents/Resources.
    private static string AssetsDirectory()
    {
        string beside = Path.Combine(AppContext.BaseDirectory, "assets");
        string bundled = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "assets"));
        return Directory.Exists(beside) || !Directory.Exists(bundled) ? beside : bundled;
    }

    // Where the earlier versions' files may sit beside the game: the executable's directory, the working
    // directory, and inside a macOS bundle both Contents/MacOS and Contents/Resources.
    private static IEnumerable<string> InstallDirectories()
    {
        string exe = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        yield return exe;
        yield return Environment.CurrentDirectory;
        if (Path.GetFileName(exe) == "MacOS") yield return Path.Combine(Path.GetDirectoryName(exe)!, "Resources");
    }

    private static GameContext Context(IRenderer renderer, SaveStore saves, string assets, IFontLoader fonts, IAudio audio,
                                       IAchievements achievements, Language? forced, Language detected)
    {
        var text = new Text(fonts, Path.Combine(assets, "fonts"), new OverlayAtlas(renderer.Atlas)) { Symbols = SymbolImages.Rasterize };
        audio.Apply(saves.Settings);
        var context = new GameContext(renderer, BoardMeshes.Upload(renderer), new Ui(text), saves, audio, achievements,
            Path.Combine(assets, "levels"))
        {
            LanguageDirectory = Path.Combine(assets, "lang"),
            ForcedLanguage = forced,
            DetectedLanguage = detected,
        };
        context.ApplyLanguage();
        return context;
    }

    private static string MusicPath(string assets) => Path.Combine(assets, "music", "soundtrack.ogg");

    private static IScreen FirstScreen(GameContext context, string? level) => level == null
        ? MenuScreen.AtStart(context)
        : new LevelScreen(context, Enum.Parse<Difficulty>(level[..^2], ignoreCase: true), int.Parse(level[^2..], CultureInfo.InvariantCulture));

    private static int RunHeadless(SaveStore saves, string assets, int width, int height, string? scriptText, string? level, double timeout,
                                   string? captureAudio, string? record, bool perf, Language? language)
    {
        using var renderer = new MetalRenderer(IntPtr.Zero, () => (width, height), vsync: false);
        MenuScreen.KeySymbol = KeyCodes.Symbol;
        MenuScreen.KeyName = KeyCodes.Name;
        // Headless, the mix goes nowhere unless it is captured to a file.
        using var audio = new AudioService(MusicPath(assets), _ => null);
        GameContext context = Context(renderer, saves, assets, new CoreTextFontLoader(), audio, new NoAchievements(), language, Language.English);
        List<float>? captured = captureAudio != null ? [] : null;
        using FileStream? recording = record != null ? new FileStream(record, FileMode.Create, FileAccess.Write) : null;
        var samples = new List<(double UpdateMs, RenderStats Render, long Allocated)>();
        int[] gcBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var block = new float[Mixer.Rate / 60 * 2];
        var input = new ScriptedInput();
        var frame = new Frame(context, FirstScreen(context, level));
        var script = new Script(scriptText ?? "", input);
        const double step = 1.0 / 60;
        double time = 0;
        while (!script.Done)
        {
            if (time > timeout)
            {
                Log.Error($"Timed out after {timeout} s");
                return 4;
            }
            script.BeforeFrame(time, frame.Screen, context);
            if (script.Error != null) break;
            if (script.Next is { } next)
            {
                frame.Show(next);
                script.Next = null;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            frame.Run(input, step, width, height);
            if (perf) samples.Add((frame.UpdateMs, renderer.Stats, GC.GetAllocatedBytesForCurrentThread() - allocated));
            script.AfterFrame(renderer);
            recording?.Write(renderer.ReadPixels());
            if (captured != null)
            {
                audio.Mixer.Render(block);
                captured.AddRange(block);
            }
            time += step;
            if (context.QuitRequested) break;
        }
        if (perf) PerfReport(samples, gcBefore);
        if (captured != null)
        {
            Wav.Write(captureAudio!, new Wav.Clip(Mixer.Rate, 2, Wav.FromFloat(captured.ToArray())));
            Log.Info($"Sound captured: {captureAudio}");
        }
        if (script.Error is { } error)
        {
            Log.Error($"Run failed: {error}");
            return 1;
        }
        return 0;
    }

    // Averages, 95th percentiles and worsts over a run's frames, skipping the first 30 (start-up, compiling).
    private static void PerfReport(List<(double UpdateMs, RenderStats Render, long Allocated)> samples, int[] gcBefore)
    {
        var frames = samples.Skip(30).ToList();
        if (frames.Count == 0) return;
        string Stat(Func<(double UpdateMs, RenderStats Render, long Allocated), double> f, string unit)
        {
            var values = frames.Select(f).OrderBy(v => v).ToList();
            return $"avg {values.Average():0.00} {unit}, p95 {values[(int) (values.Count * 0.95)]:0.00}, max {values[^1]:0.00}";
        }
        Console.WriteLine($"Frames: {frames.Count}");
        Console.WriteLine($"  update (logic, UI)   {Stat(s => s.UpdateMs, "ms")}");
        Console.WriteLine($"  render, CPU          {Stat(s => s.Render.CpuMs, "ms")}");
        Console.WriteLine($"  render, GPU          {Stat(s => s.Render.GpuMs, "ms")}");
        Console.WriteLine($"  draw calls           {Stat(s => s.Render.DrawCalls, "")}");
        Console.WriteLine($"  triangles            {Stat(s => s.Render.Triangles, "")}");
        Console.WriteLine($"  allocated per frame  {Stat(s => s.Allocated / 1024.0, "KB")}");
        Console.WriteLine($"  collections          gen0 {GC.CollectionCount(0) - gcBefore[0]}, gen1 {GC.CollectionCount(1) - gcBefore[1]}, gen2 {GC.CollectionCount(2) - gcBefore[2]}");
    }

    private static int RenderArt(SaveStore saves, string assets, string directory, Language? language)
    {
        int width = 1280, height = 720;
        using var renderer = new MetalRenderer(IntPtr.Zero, () => (width, height), vsync: false);
        using var audio = new AudioService(MusicPath(assets), _ => null);
        var text = new Text(new CoreTextFontLoader(), Path.Combine(assets, "fonts"), new OverlayAtlas(renderer.Atlas));
        GameContext context = Context(renderer, saves, assets, new CoreTextFontLoader(), audio, new NoAchievements(), language, Language.English);
        // A development tool: it reads the recorded solutions from the repository it is run in.
        string? root = Environment.CurrentDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Cubicolor.slnx"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            Console.Error.WriteLine("--art runs from within the repository");
            return 2;
        }
        string solutions = Path.Combine(root, "tests", "Cubicolor.Tests", "Fixtures", "solutions.txt");
        var art = new Art(context, text, solutions, (w, h) => (width, height) = (w, h));
        if (Environment.GetEnvironmentVariable("CUBICOLOR_ICON_VARIANTS") == "1") art.IconVariants(directory);
        else art.RenderAll(directory);
        return 0;
    }

    private static int RunWindowed(SaveStore saves, string assets, string? level, bool perf, Language? language)
    {
        AppKit.Start("Cubicolor");
        // A bundle has its icon from Info.plist; run straight from a build, the Dock gets it here.
        AppKit.SetIcon(Path.Combine(assets, "icon.png"));
        Settings settings = saves.Settings;
        var (w, h) = settings.WindowSize ?? (1280, 720);
        using var window = new MacWindow(w, h, "Cubicolor");
        var input = new MacInput(window);
        MenuScreen.KeyName = KeyCodes.Name;
        MenuScreen.KeySymbol = KeyCodes.Symbol;
        using var renderer = new MetalRenderer(window.MetalLayer, () =>
            ((int) MathF.Round(window.Width * window.PixelDensity), (int) MathF.Round(window.Height * window.PixelDensity)), settings.VSync);
        using var audio = new AudioService(MusicPath(assets), CoreAudioOutput.Start);
        using Steam? steam = Steam.Start();
        Language detected = Languages.Choose(null, steam?.GameLanguage, AppKit.PreferredLanguages());
        GameContext context = Context(renderer, saves, assets, new CoreTextFontLoader(), audio, steam ?? (IAchievements) new NoAchievements(),
            language, detected);
        Log.Info($"Language: {context.Language}");
        context.ApplyDisplay = s => window.SetFullscreen(s.Fullscreen);
        context.OpenUrl = AppKit.OpenUrl;
        if (settings.Fullscreen) window.SetFullscreen(true);
        var frame = new Frame(context, FirstScreen(context, level)) { SkipUnchanged = true };

        var clock = Stopwatch.StartNew();
        double last = 0;
        var meter = perf ? new WindowMeter() : null;
        while (!window.CloseRequested && !context.QuitRequested)
        {
            steam?.Tick();
            double now = clock.Elapsed.TotalSeconds;
            double seconds = Math.Min(now - last, 0.1);
            last = now;
            try
            {
                bool visible = window.IsVisible;
                bool drawn = frame.Run(input, seconds, renderer.Width, renderer.Height, draw: visible);
                // Presenting paces a frame that was drawn (with vsync); one that was not waits here instead: a
                // still picture until the next event or a sixtieth of a second, for controllers and timers, and a
                // hidden window a tenth, which keeps the game going (a fade, a level's sounds) at next to no cost.
                if (!visible) AppKit.WaitForEvent(0.1);
                else if (!drawn) Thread.Sleep(5);
                else if (frame.Unchanged) AppKit.WaitForEvent(1.0 / 60);
                else if (!settings.VSync) Thread.Sleep(1);
                meter?.Count(frame, visible, now);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // One bad frame is logged, not fatal.
                Log.Error($"Frame failed: {e}");
            }
        }
        saves.SaveSettings();
        return 0;
    }

    // What a window spends: how many frames were drawn, left as they were or hidden, and the process's CPU time
    // (every thread: the game's, the audio's, Metal's) against the wall clock, logged every few seconds.
    private sealed class WindowMeter
    {
        private int _drawn, _still, _hidden;
        private double _since, _updateMs;
        private TimeSpan _cpu = Process.GetCurrentProcess().TotalProcessorTime;

        public void Count(Frame frame, bool visible, double now)
        {
            if (!visible) _hidden++;
            else if (frame.Unchanged) _still++;
            else _drawn++;
            _updateMs += frame.UpdateMs;
            if (now - _since < 5) return;
            TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime;
            double wall = now - _since;
            int frames = _drawn + _still + _hidden;
            Log.Info(FormattableString.Invariant(
                $"Perf {frame.Screen.GetType().Name}: {_drawn / wall:0} drawn, {_still / wall:0} still, {_hidden / wall:0} hidden a second; update {_updateMs / Math.Max(1, frames):0.00} ms; CPU {(cpu - _cpu).TotalSeconds / wall * 100:0.0}%; {Environment.WorkingSet / 1048576} MB"));
            (_drawn, _still, _hidden, _updateMs, _since, _cpu) = (0, 0, 0, 0, now, cpu);
        }
    }
}
