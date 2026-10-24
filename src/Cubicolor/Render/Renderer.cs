using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Cubicolor.Render;

public readonly record struct MeshHandle(int Id);

// How a surface is coloured beyond its base colour. Every pattern is drawn by the scene shader from the
// object-space position, so all of them stay sharp at any size.
public enum Surface
{
    Plain = 0,
    // The key tile: a rounded plus in the accent colour on the top face.
    KeyPlus = 1,
    // The exit block: a rounded square in the accent colour on the top face.
    ExitSquare = 2,
    // The player cube: the accent colour rising up the sides and closing over the top as Progress goes from
    // 0 to 1.
    Paint = 3,
    // The same paint leaving: it drains out of the bottom first, so as Progress goes from 1 to 0 nothing is
    // left underneath for a roll to show.
    Drain = 4,
}

// One mesh to draw: where (Model, object to world), in which colours, with which pattern. Colours are linear
// RGB; Color.W is the opacity the whole mesh is drawn with.
// For the paint, PaintUp points away from the face it starts at, in the mesh's own space (zero means +y).
public readonly record struct DrawItem(MeshHandle Mesh, Matrix4x4 Model, Vector4 Color, Surface Surface = Surface.Plain,
                                       Vector4 Accent = default, float Progress = 0, Vector3 PaintUp = default);

// The light the scene is lit by: an ambient term and one directional light (Direction points towards the
// light), both linear RGB, plus a soft highlight.
public readonly record struct Lighting(Vector3 Direction, Vector3 Ambient, Vector3 Diffuse, float Specular, float Shininess);

// One frame's 3D content. Opaque items draw first; translucent ones (Color.W < 1) after, farthest first.
public sealed class Scene
{
    public Vector4 Background { get; set; }   // linear RGB, opaque
    public Matrix4x4 View { get; set; } = Matrix4x4.Identity;
    public Matrix4x4 Projection { get; set; } = Matrix4x4.Identity;
    public Lighting Lighting { get; set; }
    public List<DrawItem> Items { get; } = [];

    public Vector3 ViewDirection
    {
        get
        {
            Matrix4x4.Invert(View, out Matrix4x4 inverse);
            return Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, inverse));
        }
    }
}

// A 2D vertex for the overlay (menus, text): position in pixels from the top left, a texel in the overlay
// atlas, and a colour (linear RGB, straight alpha) the texel's coverage is multiplied by.
[StructLayout(LayoutKind.Sequential)]
public readonly record struct OverlayVertex(Vector2 Position, Vector2 TexCoord, Vector4 Color)
{
    public const int Stride = 32;
}

// The atlas the overlay samples: single-channel coverage. Region (0,0)-(1,1) texel is always fully covered,
// so a solid shape samples it.
public interface IOverlayAtlas
{
    int Size { get; }
    // Copy `coverage` (width × height bytes) into the atlas at (x, y).
    void Upload(int x, int y, int width, int height, ReadOnlySpan<byte> coverage);
}

// What every backend implements: meshes uploaded once, then a frame of scene and overlay at a time. Sizes are
// in pixels.
public interface IRenderer : IDisposable
{
    string Name { get; }
    int Width { get; }
    int Height { get; }

    MeshHandle Upload(MeshData mesh);
    IOverlayAtlas Atlas { get; }

    // Draw a frame: the scene, then the overlay's triangles (three vertices each) on top. Returns false when
    // nothing could be drawn (a window with no drawable yet).
    bool Draw(Scene scene, ReadOnlySpan<OverlayVertex> overlay);

    // The last drawn frame as RGBA bytes, row 0 at the top. For screenshots, which headless runs take.
    byte[] ReadPixels();
}
