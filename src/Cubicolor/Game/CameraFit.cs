using System;
using System.Collections.Generic;
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
    public static (Matrix4x4 View, Matrix4x4 Projection) Fit(Vector3 min, Vector3 max, float aspect, float margin, float minHeight,
                                                            Vector3? forward = null)
    {
        Vector3 centre = (min + max) / 2;
        Matrix4x4 view = Matrix4x4.CreateLookAt(centre - (forward ?? Forward) * 50, centre, Vector3.UnitY);

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

    // The same, but framing the box in `target` (pixels) of a `screen`-sized frame rather than the whole of it.
    public static (Matrix4x4 View, Matrix4x4 Projection) FitInto(Vector3 min, Vector3 max, Render.Rect target, Vector2 screen, Vector3 forward)
    {
        var (view, projection) = Fit(min, max, target.Width / target.Height, 0f, 0f, forward);
        // The fitted box in view space, from the projection's scale and offset.
        float width = 2 / projection.M11, height = 2 / projection.M22;
        float left = (-1 - projection.M41) * width / 2, top = (1 - projection.M42) * height / 2;
        float perPixel = width / target.Width;
        float frameLeft = left - target.X * perPixel, frameTop = top + target.Y * perPixel;
        return (view, Matrix4x4.CreateOrthographicOffCenter(frameLeft, frameLeft + screen.X * perPixel, frameTop - screen.Y * perPixel, frameTop, 1f, 100f));
    }

    // View and projection that put the middle of `centreOn`'s projection in the middle of the view, and make it
    // large enough for all of `contain` around that middle, with `margin` of the view's height free at the
    // edges. Used for the board: its tiles sit in the middle of the screen, whatever the stack beside them.
    public static (Matrix4x4 View, Matrix4x4 Projection) FitCentred(IReadOnlyList<Vector3> centreOn, IReadOnlyList<Vector3> contain,
                                                                    float aspect, float margin, float minHeight)
    {
        Vector3 centre = Vector3.Zero;
        foreach (Vector3 p in centreOn) centre += p;
        centre /= centreOn.Count;
        Matrix4x4 view = Matrix4x4.CreateLookAt(centre - Forward * 50, centre, Vector3.UnitY);

        var (low, high) = Extent(centreOn, view);
        Vector2 middle = (low + high) / 2;
        var (allLow, allHigh) = Extent(contain, view);
        // Half the view must reach from the middle to the farthest thing on either side.
        float halfWidth = MathF.Max(middle.X - allLow.X, allHigh.X - middle.X);
        float halfHeight = MathF.Max(middle.Y - allLow.Y, allHigh.Y - middle.Y);
        float height = MathF.Max(MathF.Max(2 * halfHeight, 2 * halfWidth / aspect) / (1 - 2 * margin), minHeight);
        float width = height * aspect;
        Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(
            middle.X - width / 2, middle.X + width / 2, middle.Y - height / 2, middle.Y + height / 2, 1f, 100f);
        return (view, projection);
    }

    private static (Vector2 Low, Vector2 High) Extent(IReadOnlyList<Vector3> points, Matrix4x4 view)
    {
        var low = new Vector2(float.MaxValue);
        var high = new Vector2(float.MinValue);
        foreach (Vector3 p in points)
        {
            Vector3 v = Vector3.Transform(p, view);
            low = Vector2.Min(low, new Vector2(v.X, v.Y));
            high = Vector2.Max(high, new Vector2(v.X, v.Y));
        }
        return (low, high);
    }
}
