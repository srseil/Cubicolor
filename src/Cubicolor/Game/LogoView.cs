using System;
using System.Numerics;
using Cubicolor.Render;

namespace Cubicolor.Game;

// The logo under the title: a row of tiles, a green key and lock, and the black cube rolling off the end, as
// in the logo of the earlier versions. Drawn with the game's own meshes, so it is sharp at any size.
public sealed class LogoView(BoardMeshes meshes)
{
    private double _time;

    public void Advance(double seconds) => _time += seconds;

    public void Draw(Scene scene, Rect target, Vector2 screen)
    {
        var min = new Vector3(-0.5f, -0.5f, -0.5f);
        var max = new Vector3(4.5f, 1.2f, 0.5f);
        // Nearly from the front, a little from above and the right, as the logo was rendered.
        (scene.View, scene.Projection) = CameraFit.FitInto(min, max, target, screen, Vector3.Normalize(new Vector3(-0.3f, -0.45f, -1f)));

        Matrix4x4 At(float x) => Matrix4x4.CreateTranslation(x, -0.25f, 0);
        scene.Items.Add(new DrawItem(meshes.Slab, At(0), Palette.White.WithAlpha(0.45f), Surface.KeyPlus, Palette.Green));
        scene.Items.Add(new DrawItem(meshes.Slab, At(1), Palette.White));
        scene.Items.Add(new DrawItem(meshes.Slab, At(2), Palette.Green));
        scene.Items.Add(new DrawItem(meshes.Slab, At(3), Palette.White));
        scene.Items.Add(new DrawItem(meshes.Slab, At(4), Palette.White));

        // The cube rocks over the edge between the last two tiles.
        float angle = 28f + 4f * MathF.Sin((float) _time * 1.3f);
        var pivot = new Vector3(3.5f, 0, 0);
        Matrix4x4 cube = Matrix4x4.CreateTranslation(new Vector3(3f, 0.5f, 0) - pivot)
                         * Matrix4x4.CreateRotationZ(-angle * MathF.PI / 180)
                         * Matrix4x4.CreateTranslation(pivot);
        scene.Items.Add(new DrawItem(meshes.Cube, cube, Palette.Black, Surface.Paint, Palette.Black, 0));
    }
}
