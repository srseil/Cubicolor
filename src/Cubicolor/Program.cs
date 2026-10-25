using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
          --data-dir <dir>        where progress and settings live (headless default: a fresh temporary one)
          --timeout <seconds>     fail a run that takes longer
          --capture-audio <wav>   headless: write what the game played to a WAV file
          --art <dir>             render the Steam store and library images, the icon and screenshots into <dir>
        """;

    private static int Main(string[] args)
    {
        bool headless = false;
        string? script = null, level = null, dataDir = null, captureAudio = null, art = null;
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
                    case "--data-dir": dataDir = args[++i]; break;
                    case "--capture-audio": captureAudio = args[++i]; break;
                    case "--art": art = args[++i]; headless = true; break;
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
            if (art != null) return RenderArt(saves, assets, art);
            return headless
                ? RunHeadless(saves, assets, width, height, script, level, timeout, captureAudio)
                : RunWindowed(saves, assets, level);
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
                                       IAchievements achievements)
    {
        var text = new Text(fonts, Path.Combine(assets, "fonts"), new OverlayAtlas(renderer.Atlas)) { Symbols = SymbolImages.Rasterize };
        audio.Apply(saves.Settings);
        return new GameContext(renderer, BoardMeshes.Upload(renderer), new Ui(text), saves, audio, achievements,
            Path.Combine(assets, "levels"));
    }

    private static string MusicPath(string assets) => Path.Combine(assets, "music", "soundtrack.qoa");

    private static IScreen FirstScreen(GameContext context, string? level) => level == null
        ? new MenuScreen(context)
        : new LevelScreen(context, Enum.Parse<Difficulty>(level[..^2], ignoreCase: true), int.Parse(level[^2..], CultureInfo.InvariantCulture));

    private static int RunHeadless(SaveStore saves, string assets, int width, int height, string? scriptText, string? level, double timeout,
                                   string? captureAudio)
    {
        using var renderer = new MetalRenderer(IntPtr.Zero, () => (width, height), vsync: false);
        // Headless, the mix goes nowhere unless it is captured to a file.
        using var audio = new AudioService(MusicPath(assets), _ => null);
        GameContext context = Context(renderer, saves, assets, new CoreTextFontLoader(), audio, new NoAchievements());
        List<float>? captured = captureAudio != null ? [] : null;
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
            frame.Run(input, step, width, height);
            script.AfterFrame(renderer);
            if (captured != null)
            {
                audio.Mixer.Render(block);
                captured.AddRange(block);
            }
            time += step;
            if (context.QuitRequested) break;
        }
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

    private static int RenderArt(SaveStore saves, string assets, string directory)
    {
        int width = 1280, height = 720;
        using var renderer = new MetalRenderer(IntPtr.Zero, () => (width, height), vsync: false);
        using var audio = new AudioService(MusicPath(assets), _ => null);
        var text = new Text(new CoreTextFontLoader(), Path.Combine(assets, "fonts"), new OverlayAtlas(renderer.Atlas));
        GameContext context = Context(renderer, saves, assets, new CoreTextFontLoader(), audio, new NoAchievements());
        // A development tool: it reads the recorded solutions from the repository it is run in.
        string? root = Environment.CurrentDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Cubicolor.slnx"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            Console.Error.WriteLine("--art runs from within the repository");
            return 2;
        }
        string solutions = Path.Combine(root, "tests", "Cubicolor.Tests", "Fixtures", "solutions.txt");
        new Art(context, text, solutions, (w, h) => (width, height) = (w, h)).RenderAll(directory);
        return 0;
    }

    private static int RunWindowed(SaveStore saves, string assets, string? level)
    {
        AppKit.Start("Cubicolor");
        Settings settings = saves.Settings;
        var (w, h) = settings.WindowSize ?? (1280, 720);
        using var window = new MacWindow(w, h, "Cubicolor");
        var input = new MacInput(window);
        MenuScreen.KeyName = KeyCodes.Name;
        using var renderer = new MetalRenderer(window.MetalLayer, () =>
            ((int) MathF.Round(window.Width * window.PixelDensity), (int) MathF.Round(window.Height * window.PixelDensity)), settings.VSync);
        using var audio = new AudioService(MusicPath(assets), CoreAudioOutput.Start);
        using Steam? steam = Steam.Start();
        GameContext context = Context(renderer, saves, assets, new CoreTextFontLoader(), audio, steam ?? (IAchievements) new NoAchievements());
        context.ApplyDisplay = s => window.SetFullscreen(s.Fullscreen);
        context.OpenUrl = AppKit.OpenUrl;
        if (settings.Fullscreen) window.SetFullscreen(true);
        var frame = new Frame(context, FirstScreen(context, level));

        var clock = Stopwatch.StartNew();
        double last = 0;
        while (!window.CloseRequested && !context.QuitRequested)
        {
            steam?.Tick();
            double now = clock.Elapsed.TotalSeconds;
            double seconds = Math.Min(now - last, 0.1);
            last = now;
            try
            {
                if (!frame.Run(input, seconds, renderer.Width, renderer.Height)) Thread.Sleep(5);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // One bad frame is logged, not fatal.
                Log.Error($"Frame failed: {e}");
            }
            if (!settings.VSync) Thread.Sleep(1);
        }
        saves.SaveSettings();
        return 0;
    }
}
