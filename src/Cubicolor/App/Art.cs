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
    private readonly Overlay _underlay = new();

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
        Icon(directory, "icon_macos", 1024, IconKind.MacOS);
        Icon(directory, "icon", 1024, IconKind.Plain);
        Icon(directory, "shortcut_icon", 256, IconKind.Plain);
        Icon(directory, "app_icon", 184, IconKind.Square);
        MacIconSet(Path.Combine(directory, "Cubicolor.iconset"));
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

    private enum IconKind { Plain, MacOS, Square }

    // How the macOS icon's rounded square is drawn: flat, or as a plaque with depth.
    public enum Plaque { Flat, Edged, Raised, Glint }

    public Plaque IconPlaque { get; set; } = Plaque.Flat;

    // The rounded square under the cube on macOS, in Apple's grid. Edged: a front edge below it as the game's
    // tiles have, and light falling from above across its face. Raised: also a bevelled rim, a soft shadow
    // under the whole and a faint one under the cube.
    private void DrawPlaque(int size, float margin, float body)
    {
        float radius = size * 185f / 1024;
        var face = new Rect(margin, margin, body, body);
        if (IconPlaque == Plaque.Flat)
        {
            _underlay.RoundedRectangle(face, radius, Palette.Background);
            return;
        }
        float edge = body * 0.035f;
        var top = new Rect(margin, margin, body, body - edge);
        if (IconPlaque == Plaque.Raised)
            for (int i = 6; i >= 1; i--)
            {
                float spread = body * 0.008f * i;
                var shadow = new Rect(margin - spread, margin + edge * 0.6f - spread * 0.4f, body + 2 * spread, body + 2 * spread);
                _underlay.RoundedRectangle(shadow, radius + spread, new Vector4(0, 0, 0, 0.035f));
            }
        _underlay.RoundedRectangle(face, radius, Palette.Hex(0xc9c5bd));
        if (IconPlaque == Plaque.Raised)
        {
            float rim = body * 0.012f;
            _underlay.RoundedRectangle(top, radius, Palette.Hex(0xffffff), Palette.Hex(0xd8d4cc));
            top = top.Inset(rim);
            radius -= rim;
        }
        // Under a glint, the face is a shade darker, so the light has something to show against.
        if (IconPlaque == Plaque.Glint) _underlay.RoundedRectangle(top, radius, Palette.Hex(0xeeebe5), Palette.Hex(0xdedad2));
        else _underlay.RoundedRectangle(top, radius, Palette.Hex(0xf7f5f1), Palette.Hex(0xe9e6df));
        if (IconPlaque == Plaque.Glint)
            text.DrawImage(_underlay, $"glint {size}", Glint(top, radius), new Vector2(top.X, top.Y), new Vector4(1, 1, 1, 1));
        if (IconPlaque == Plaque.Raised)
        {
            // Where the cube stands: a soft dark ellipse.
            float w = body * 0.5f, h = body * 0.16f;
            for (int i = 4; i >= 1; i--)
            {
                var spot = new Rect(size * 0.465f - w / 2 - i * body * 0.01f, size * 0.675f - h / 2 - i * body * 0.006f, w + i * body * 0.02f, h + i * body * 0.012f);
                _underlay.RoundedRectangle(spot, spot.Height / 2, new Vector4(0, 0, 0, 0.03f));
            }
        }
    }

    // The app icon: the black cube on its own at the game's angle, as the earlier versions' icon was. On macOS it
    // sits on a rounded square in the game's background colour (Apple's grid: an 824 body on a 1024 canvas,
    // corners of 185); elsewhere it stands on nothing, or on a square of the background.
    private void Icon(string directory, string name, int size, IconKind kind)
    {
        _scene.Items.Clear();
        _overlay.Clear();
        _underlay.Clear();
        _scene.Background = kind == IconKind.Square ? Palette.Background : Palette.Background with { W = 0 };
        _scene.Lighting = GameContext.Light;
        float body = kind == IconKind.MacOS ? size * 824f / 1024 : size;
        float margin = (size - body) / 2;
        if (kind == IconKind.MacOS) DrawPlaque(size, margin, body);
        float cube = kind == IconKind.Plain ? size * 0.92f : body * 0.64f;
        var target = new Rect((size - cube) / 2, (size - cube) / 2, cube, cube);
        (_scene.View, _scene.Projection) = CameraFit.FitInto(new Vector3(-0.5f), new Vector3(0.5f), target, new Vector2(size), CameraFit.Forward);
        _scene.Items.Add(new DrawItem(context.Meshes.Cube, Matrix4x4.Identity, Palette.Black));
        Save(directory, name, size, size, opaque: kind == IconKind.Square);
    }

    // Light catching the plaque from the top right: a soft pool in that corner and a fine highlight along the
    // top and right rims, both fading away from it, clipped to the plaque's rounded shape.
    private static GlyphInk Glint(Rect face, float radius)
    {
        int width = (int) MathF.Ceiling(face.Width), height = (int) MathF.Ceiling(face.Height);
        var coverage = new byte[width * height];
        float body = face.Width;
        var corner = new Vector2(width - radius * 0.6f, radius * 0.6f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            // Signed distance to the rounded rectangle's edge, negative inside.
            Vector2 q = Vector2.Abs(p - new Vector2(width, height) / 2) - new Vector2(width, height) / 2 + new Vector2(radius);
            float edge = Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) - radius;
            float inside = Math.Clamp(0.5f - edge, 0, 1);
            if (inside <= 0) continue;
            float fromCorner = Vector2.Distance(p, corner) / body;
            float pool = 0.75f * MathF.Exp(-fromCorner / 0.28f);
            // The rim: within a sliver of the top or right edge, strongest near the corner.
            float rimWidth = body * 0.012f;
            float rim = Math.Clamp(1 - (-edge) / rimWidth, 0, 1) * 0.9f * MathF.Exp(-fromCorner / 0.45f);
            // The rim lights the top and right sides, fading in by how much a point lies towards the lit corner.
            Vector2 centre = new Vector2(width, height) / 2;
            float towards = Vector2.Dot(Vector2.Normalize(p - centre + new Vector2(1e-3f)), Vector2.Normalize(corner - centre));
            float side = Math.Clamp((towards + 0.2f) / 0.8f, 0, 1);
            side = side * side * (3 - 2 * side);
            float light = pool + rim * side;
            // A little noise before rounding to 8 bits, so the soft gradient doesn't band.
            float dither = (((uint) (x * 73 + y * 151) * 2654435761u) >> 24) / 255f - 0.5f;
            coverage[y * width + x] = (byte) Math.Clamp(light * inside * 255 + dither, 0, 255);
        }
        return new GlyphInk(width, height, coverage, 0, 0);
    }

    // The macOS icon in each plaque style, side by side for choosing.
    public void IconVariants(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (Plaque plaque in Enum.GetValues<Plaque>())
        {
            IconPlaque = plaque;
            Icon(directory, $"icon_macos_{plaque.ToString().ToLowerInvariant()}", 1024, IconKind.MacOS);
        }
        IconPlaque = Plaque.Flat;
    }

    // The macOS icon set: every size iconutil wants, from which packaging makes Cubicolor.icns.
    public void MacIconSet(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (int points in new[] { 16, 32, 128, 256, 512 })
        {
            Icon(directory, $"icon_{points}x{points}", points, IconKind.MacOS);
            Icon(directory, $"icon_{points}x{points}@2x", points * 2, IconKind.MacOS);
        }
    }

    private void Save(string directory, string name, int width, int height, bool opaque)
    {
        resize(width, height);
        context.Renderer.Draw(_scene, _underlay.Vertices, _overlay.Vertices);
        _underlay.Clear();
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
