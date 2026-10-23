using System;
using System.IO;
using Cubicolor.Game;
using Cubicolor.Platform.Metal;
using Cubicolor.Render;
using Cubicolor.Simulation.Levels;
using Cubicolor.Utilities;

namespace Cubicolor;

internal static class Program
{
    private static int Main(string[] args)
    {
        string level = "normal03";
        string output = "scratch/frame.png";
        string moves = "";
        double extra = 0;
        int width = 1280, height = 720;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--level": level = args[++i]; break;
                case "--screenshot": output = args[++i]; break;
                case "--moves": moves = args[++i]; break;
                case "--time": extra = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--size":
                    string[] parts = args[++i].Split('x');
                    width = int.Parse(parts[0]);
                    height = int.Parse(parts[1]);
                    break;
            }
        }

        var difficulty = Enum.Parse<Difficulty>(level[..^2], ignoreCase: true);
        Level loaded = LevelFile.Load(Path.Combine(AppContext.BaseDirectory, "assets", "levels"), difficulty, int.Parse(level[^2..]));
        using var renderer = new MetalRenderer(IntPtr.Zero, () => (width, height), vsync: false);
        var meshes = BoardMeshes.Upload(renderer);
        var puzzle = new Puzzle(loaded);
        var board = new BoardView(loaded, meshes);
        board.Start(puzzle);
        const double step = 1.0 / 60;
        while (!board.Ready()) board.Advance(step);
        foreach (char c in moves)
        {
            Direction direction = c switch { 'F' => Direction.Forwards, 'B' => Direction.Backwards, 'L' => Direction.Left, _ => Direction.Right };
            board.Show(puzzle.Roll(direction));
            do board.Advance(step); while (!board.Ready());
        }
        for (double t = 0; t < extra; t += step) board.Advance(step);

        var scene = new Scene
        {
            Background = Palette.Background,
            Lighting = new Lighting(new(0.27f, 1f, 0.6f), new(0.233f), new(0.574f), 0.09f, 8f),
        };
        var (min, max) = board.Bounds();
        (scene.View, scene.Projection) = CameraFit.Fit(min, max, (float) width / height, 0.12f, 6f);
        board.Draw(scene);
        renderer.Draw(scene, ReadOnlySpan<OverlayVertex>.Empty);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        Png.Write(output, renderer.ReadPixels(), renderer.Width, renderer.Height, bgra: false, opaque: true);
        Log.Info($"Wrote {output}");
        return 0;
    }
}
