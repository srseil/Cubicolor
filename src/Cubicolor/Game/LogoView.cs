using System;
using System.Numerics;
using Cubicolor.Render;

namespace Cubicolor.Game;

// The logo of the earlier versions, drawn with the game's own meshes so it is sharp at any size: a row of five
// tiles seen from the front and steeply above, the first three falling away behind the cube (the key with
// its faded plus lowest and faintest, then a white tile, then the green lock just starting to drop), and the
// black cube rolling onto the last tile, rocking gently.
public sealed class LogoView(BoardMeshes meshes)
{
    private double _time;

    // Measured off the logo image: how far each falling tile has dropped and how much of it is left.
    private static readonly (float Drop, float Alpha)[] Falling = [(0.6f, 0.35f), (0.35f, 0.7f), (0.13f, 0.8f)];

    // The logo's view: straight from the front, 59 degrees down, so the row runs level across it.
    private static readonly Vector3 Forward = Vector3.Normalize(new Vector3(0, -MathF.Sin(59 * MathF.PI / 180), -MathF.Cos(59 * MathF.PI / 180)));

    public void Advance(double seconds) => _time += seconds;

    public void Draw(Scene scene, Rect target, Vector2 screen, float alpha = 1)
    {
        var min = new Vector3(-0.5f, -1.1f, -0.5f);
        var max = new Vector3(5.1f, 1.4f, 0.5f);
        (scene.View, scene.Projection) = CameraFit.FitInto(min, max, target, screen, Forward);
        int first = scene.Items.Count;

        Matrix4x4 At(float x, float drop) => Matrix4x4.CreateTranslation(x, -0.25f - drop, 0);
        var (drop0, alpha0) = Falling[0];
        var (drop1, alpha1) = Falling[1];
        var (drop2, alpha2) = Falling[2];
        scene.Items.Add(new DrawItem(meshes.Slab, At(0, drop0), Palette.White.WithAlpha(alpha0), Surface.KeyPlus, Palette.Green.WithAlpha(0.6f)));
        scene.Items.Add(new DrawItem(meshes.Slab, At(1, drop1), Palette.White.WithAlpha(alpha1)));
        scene.Items.Add(new DrawItem(meshes.Slab, At(2, drop2), Palette.Green.WithAlpha(alpha2)));
        scene.Items.Add(new DrawItem(meshes.Slab, At(3, 0), Palette.White));
        scene.Items.Add(new DrawItem(meshes.Slab, At(4, 0), Palette.White));

        // Rolling onto the last tile: tipped 28 degrees back over the edge it comes across (fitted to the logo's
        // outline), rocking a few degrees either way.
        float angle = 28f + 3f * MathF.Sin((float) _time * 1.3f);
        var pivot = new Vector3(3.5f, 0, 0);
        Matrix4x4 cube = Matrix4x4.CreateTranslation(new Vector3(4f, 0.5f, 0) - pivot)
                         * Matrix4x4.CreateRotationZ(angle * MathF.PI / 180)
                         * Matrix4x4.CreateTranslation(pivot);
        scene.Items.Add(new DrawItem(meshes.Cube, cube, Palette.Black));

        if (alpha < 1)
            for (int i = first; i < scene.Items.Count; i++)
                scene.Items[i] = scene.Items[i] with { Color = scene.Items[i].Color with { W = scene.Items[i].Color.W * alpha } };
    }
}
