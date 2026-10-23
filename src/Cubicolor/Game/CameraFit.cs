using System;
using System.Numerics;

namespace Cubicolor.Game;

// The camera: orthographic, looking down at 60 degrees and turned 20 degrees, as both earlier versions set it
// up. Framing is fitted to what is on the board: the bounds' projection is centred in the view with a margin
// on every side. (The Godot version placed the camera by a formula of the board's size and the stack's height
// instead, which left most levels off centre.)
public static class CameraFit
{
    // The direction the camera looks: Godot's rotate_x(-60) then rotate_y(20) applied to -z.
    public static readonly Vector3 Forward = Vector3.Normalize(new Vector3(
        -MathF.Sin(20 * MathF.PI / 180) * MathF.Cos(60 * MathF.PI / 180),
        -MathF.Sin(60 * MathF.PI / 180),
        -MathF.Cos(20 * MathF.PI / 180) * MathF.Cos(60 * MathF.PI / 180)));

    // View and projection showing the box from `min` to `max` in a viewport of `aspect` (width / height), with
    // `margin` of the view's height free above and below it, never zoomed in further than `minHeight` world
    // units of view height.
    public static (Matrix4x4 View, Matrix4x4 Projection) Fit(Vector3 min, Vector3 max, float aspect, float margin, float minHeight)
    {
        Vector3 centre = (min + max) / 2;
        Matrix4x4 view = Matrix4x4.CreateLookAt(centre - Forward * 50, centre, Vector3.UnitY);

        var low = new Vector2(float.MaxValue);
        var high = new Vector2(float.MinValue);
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vector3((corner & 1) == 0 ? min.X : max.X, (corner & 2) == 0 ? min.Y : max.Y, (corner & 4) == 0 ? min.Z : max.Z);
            Vector3 v = Vector3.Transform(point, view);
            low = Vector2.Min(low, new Vector2(v.X, v.Y));
            high = Vector2.Max(high, new Vector2(v.X, v.Y));
        }
        Vector2 size = high - low;
        Vector2 middle = (low + high) / 2;
        float height = MathF.Max(MathF.Max(size.Y, size.X / aspect) / (1 - 2 * margin), minHeight);
        float width = height * aspect;
        Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(
            middle.X - width / 2, middle.X + width / 2, middle.Y - height / 2, middle.Y + height / 2, 1f, 100f);
        return (view, projection);
    }
}
