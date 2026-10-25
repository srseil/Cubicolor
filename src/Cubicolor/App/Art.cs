using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Cubicolor.Game;
using Cubicolor.Render;
using Cubicolor.Simulation.Levels;
using Cubicolor.Utilities;

namespace Cubicolor.App;

// The Steam store's and library's images, rendered by the game itself at each one's exact size (sizes from
// partner.steamgames.com/doc/store/assets, 2026-10-23), plus the app icon and screenshots. A level part-way
// through its recorded solution, with the cube painted, is the picture; the title is set in Vollkorn Black.
internal sealed class Art(GameContext context, Text text, string solutions, Action<int, int> resize)
{
    private readonly Scene _scene = new();
    private readonly Overlay _overlay = new();

    public void RenderAll(string directory)
    {
        Directory.CreateDirectory(directory);
        // Capsules: title and board side by side, or stacked when the image is tall.
        Capsule(directory, "header_capsule", 920, 430, "genius05");
        Capsule(directory, "small_capsule", 462, 174, "smart04");
        Capsule(directory, "main_capsule", 1232, 706, "genius05");
        Capsule(directory, "vertical_capsule", 748, 896, "genius12");
        Capsule(directory, "library_capsule", 600, 900, "genius12");
        Capsule(directory, "library_header", 920, 430, "genius05");
        Capsule(directory, "page_background", 1438, 810, "genius15", title: false, faded: true);
        Capsule(directory, "library_hero", 3840, 1240, "genius15", title: false);
        Capsule(directory, "event_cover", 800, 450, "smart09");
        Logo(directory, "library_logo", 1280, 720);
        Icon(directory, "icon", 1024);
        Icon(directory, "shortcut_icon", 256);
        Icon(directory, "app_icon", 184, background: true);
        foreach (string level in new[] { "normal03", "smart09", "genius05", "genius12", "genius15" })
            Capsule(directory, $"screenshot_{level}", 1920, 1080, level, title: false, hud: true);
    }

    private BoardView Board(string name, double fraction)
    {
        var difficulty = Enum.Parse<Difficulty>(name[..^2], ignoreCase: true);
        int number = int.Parse(name[^2..]);
        Level level = context.Load(difficulty, number);
        string moves = File.ReadLines(solutions).First(l => l.StartsWith(name + ".bin", StringComparison.Ordinal)).Split(' ')[1];
        var puzzle = new Puzzle(level);
        var board = new BoardView(level, context.Meshes);
        board.Start(puzzle);
        board.Advance(board.ReadyAt - board.Now);
        // Stop part-way, on a move that leaves the cube painted, so the colours show.
        int stop = (int) (moves.Length * fraction);
        for (int i = 0; i < moves.Length && (i < stop || puzzle.Carried == Paint.None); i++)
        {
            board.Show(puzzle.Roll(moves[i] switch { 'F' => Direction.Forwards, 'B' => Direction.Backwards, 'L' => Direction.Left, _ => Direction.Right }));
            board.Advance(BoardView.Roll + 0.05);
            if (i >= stop && puzzle.Carried != Paint.None) break;
        }
        board.Advance(3);
        return board;
    }

    private void Capsule(string directory, string name, int width, int height, string level,
                         bool title = true, bool faded = false, bool hud = false)
    {
        _scene.Items.Clear();
        _overlay.Clear();
        _scene.Background = Palette.Background;
        _scene.Lighting = GameContext.Light;
        BoardView board = Board(level, 0.45);
        var (min, max) = board.Bounds();
        bool tall = height > width;
        Rect area;
        if (!title) area = new Rect(0, 0, width, height);
        else if (tall) area = new Rect(width * 0.05f, height * 0.3f, width * 0.9f, height * 0.66f);
        else area = new Rect(width * 0.46f, height * 0.06f, width * 0.5f, height * 0.88f);
        if (!title)
        {
            var (view, projection) = CameraFit.Fit(min, max, (float) width / height, hud ? 0.1f : 0.06f, 6f);
            (_scene.View, _scene.Projection) = (view, projection);
        }
        else
        {
            (_scene.View, _scene.Projection) = CameraFit.FitInto(min, max, area, new Vector2(width, height), CameraFit.Forward);
        }
        board.Draw(_scene);
        if (faded)
            // A page background sits behind the store's text: the board only faintly there.
            for (int i = 0; i < _scene.Items.Count; i++) _scene.Items[i] = _scene.Items[i] with { Color = _scene.Items[i].Color with { W = 0.25f } };

        if (title)
        {
            // As large as fits its part: the whole width less a margin when stacked, the left 40 % beside the board.
            float room = tall ? width * 0.86f : width * 0.4f;
            float size = MathF.Min(tall ? height * 0.14f : height * 0.3f, room / text.Measure("Cubicolor", FontStyle.Black, 100) * 100);
            float textWidth = text.Measure("Cubicolor", FontStyle.Black, size);
            float x = tall ? (width - textWidth) / 2 : width * 0.05f + (room - textWidth) / 2;
            float y = tall ? height * 0.08f : (height - text.LineHeight(FontStyle.Black, size)) / 2;
            text.Draw(_overlay, "Cubicolor", new Vector2(x, y), FontStyle.Black, size, Palette.Text);
        }
        if (hud)
        {
            float scale = height / 720f;
            text.Draw(_overlay, LevelName(level).Difficulty, new Vector2(40 * scale, 24 * scale), FontStyle.Regular, 42 * scale, Palette.Text);
            text.Draw(_overlay, $"Level {LevelName(level).Number}", new Vector2(42 * scale, 76 * scale), FontStyle.Regular, 30 * scale, Palette.Text);
        }
        Save(directory, name, width, height, opaque: true);
    }

    private static (string Difficulty, int Number) LevelName(string level) =>
        (char.ToUpperInvariant(level[0]) + level[1..^2], int.Parse(level[^2..]));

    // The title over the logo's row of tiles, on nothing: the library lays it over the hero image.
    private void Logo(string directory, string name, int width, int height)
    {
        _scene.Items.Clear();
        _overlay.Clear();
        _scene.Background = Palette.Background with { W = 0 };
        _scene.Lighting = GameContext.Light;
        float size = height * 0.36f;
        float textWidth = text.Measure("Cubicolor", FontStyle.Black, size);
        text.Draw(_overlay, "Cubicolor", new Vector2((width - textWidth) / 2, height * 0.08f), FontStyle.Black, size, Palette.Text);
        var logo = new LogoView(context.Meshes);
        logo.Draw(_scene, new Rect(width * 0.25f, height * 0.6f, width * 0.5f, height * 0.36f), new Vector2(width, height));
        Save(directory, name, width, height, opaque: false);
    }

    // The black cube, half painted, on its tile.
    private void Icon(string directory, string name, int size, bool background = false)
    {
        _scene.Items.Clear();
        _overlay.Clear();
        _scene.Background = background ? Palette.Background : Palette.Background with { W = 0 };
        _scene.Lighting = GameContext.Light;
        var min = new Vector3(-0.55f, -0.5f, -0.55f);
        var max = new Vector3(0.55f, 1f, 0.55f);
        (_scene.View, _scene.Projection) = CameraFit.FitInto(min, max, new Rect(size * 0.04f, size * 0.04f, size * 0.92f, size * 0.92f),
            new Vector2(size), CameraFit.Forward);
        _scene.Items.Add(new DrawItem(context.Meshes.Slab, Matrix4x4.CreateTranslation(0, -0.25f, 0), Palette.White));
        _scene.Items.Add(new DrawItem(context.Meshes.Cube, Matrix4x4.CreateTranslation(0, 0.5f, 0), Palette.Black, Surface.Paint, Palette.Green, 0.42f));
        Save(directory, name, size, size, opaque: background);
    }

    private void Save(string directory, string name, int width, int height, bool opaque)
    {
        resize(width, height);
        context.Renderer.Draw(_scene, [], _overlay.Vertices);
        string path = Path.Combine(directory, name + ".png");
        byte[] pixels = context.Renderer.ReadPixels();
        if (!opaque)
            // The frame holds premultiplied colour; PNG wants it straight.
            for (int i = 0; i < pixels.Length; i += 4)
                if (pixels[i + 3] is > 0 and < 255 and var a)
                    for (int c = 0; c < 3; c++) pixels[i + c] = (byte) Math.Min(255, pixels[i + c] * 255 / a);
        Png.Write(path, pixels, width, height, bgra: false, opaque: opaque);
        Log.Info($"Art: {path} ({width}x{height})");
    }
}
