using System;
using System.Collections.Generic;
using System.Numerics;

namespace Cubicolor.Render;

// A rectangle in pixels, top-left origin.
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Vector2 Centre => new(X + Width / 2, Y + Height / 2);
    public bool Contains(Vector2 p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
    public Rect Inset(float d) => new(X + d, Y + d, Width - 2 * d, Height - 2 * d);
}

// One frame's 2D overlay as triangles: solid shapes (sampling the atlas's covered block) and text (sampling
// glyphs). The overlay is drawn into the multisampled target, so shape edges are antialiased like the scene.
public sealed class Overlay
{
    private readonly List<OverlayVertex> _vertices = [];

    public ReadOnlySpan<OverlayVertex> Vertices => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices);

    public void Clear() => _vertices.Clear();

    public void Triangle(Vector2 a, Vector2 b, Vector2 c, Vector4 color)
    {
        Vector2 uv = OverlayAtlas.Solid;
        _vertices.Add(new OverlayVertex(a, uv, color));
        _vertices.Add(new OverlayVertex(b, uv, color));
        _vertices.Add(new OverlayVertex(c, uv, color));
    }

    public void Rectangle(Rect r, Vector4 color)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        var a = new Vector2(r.X, r.Y);
        var b = new Vector2(r.Right, r.Y);
        var c = new Vector2(r.Right, r.Bottom);
        var d = new Vector2(r.X, r.Bottom);
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }

    // A rectangle with every corner rounded by `radius`: a fan around its centre.
    public void RoundedRectangle(Rect r, float radius, Vector4 color)
    {
        radius = MathF.Min(radius, MathF.Min(r.Width, r.Height) / 2);
        if (radius <= 0.5f)
        {
            Rectangle(r, color);
            return;
        }
        const int perCorner = 6;
        Vector2 centre = r.Centre;
        Span<Vector2> outline = stackalloc Vector2[4 * (perCorner + 1)];
        int n = 0;
        (Vector2 Corner, float Start)[] corners =
        [
            (new Vector2(r.Right - radius, r.Y + radius), -MathF.PI / 2),
            (new Vector2(r.Right - radius, r.Bottom - radius), 0),
            (new Vector2(r.X + radius, r.Bottom - radius), MathF.PI / 2),
            (new Vector2(r.X + radius, r.Y + radius), MathF.PI),
        ];
        foreach (var (corner, start) in corners)
            for (int i = 0; i <= perCorner; i++)
            {
                float angle = start + MathF.PI / 2 * i / perCorner;
                outline[n++] = corner + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
        for (int i = 0; i < n; i++) Triangle(centre, outline[i], outline[(i + 1) % n], color);
    }

    // A textured quad from the atlas, its coverage tinting `color`.
    public void Image(Rect r, Vector2 uvTopLeft, Vector2 uvBottomRight, Vector4 color)
    {
        var a = new OverlayVertex(new Vector2(r.X, r.Y), uvTopLeft, color);
        var b = new OverlayVertex(new Vector2(r.Right, r.Y), new Vector2(uvBottomRight.X, uvTopLeft.Y), color);
        var c = new OverlayVertex(new Vector2(r.Right, r.Bottom), uvBottomRight, color);
        var d = new OverlayVertex(new Vector2(r.X, r.Bottom), new Vector2(uvTopLeft.X, uvBottomRight.Y), color);
        _vertices.AddRange([a, b, c, a, c, d]);
    }
}
