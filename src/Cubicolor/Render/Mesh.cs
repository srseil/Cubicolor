using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Cubicolor.Render;

// A vertex as every backend's scene shader reads it: object-space position and normal. Surface patterns
// (the key's plus, the exit's square, the paint spreading over the cube) are computed from the object-space
// position in the shader, so there are no texture coordinates.
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MeshVertex(Vector3 Position, Vector3 Normal)
{
    public const int Stride = 24;
}

public sealed record MeshData(MeshVertex[] Vertices, ushort[] Indices);

// The game's shapes, generated: a box with rounded edges and corners. Both earlier versions loaded these as
// models (cube_bevel.obj, ground_bevel.obj); generated, they have smooth normals everywhere and any number of
// segments.
public static class RoundedBox
{
    // A box of `size` centred on the origin, every edge rounded with `radius`, each rounding made of
    // `segments` steps. Every face is a grid whose lines bunch up in the rounded band at its border; each grid
    // point is pushed out from the box's inner core (the box shrunk by the radius) to the radius, which gives
    // flat faces, quarter-cylinder edges and eighth-sphere corners with exact normals.
    public static MeshData Build(Vector3 size, float radius, int segments)
    {
        Vector3 half = size / 2;
        if (radius <= 0 || radius > MathF.Min(half.X, MathF.Min(half.Y, half.Z)))
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Must be positive and fit the box");
        Vector3 core = half - new Vector3(radius);

        var vertices = new List<MeshVertex>();
        var indices = new List<ushort>();

        // Each face: its normal axis and sign, and the two axes spanning it, ordered so that u × v points out
        // of the box. Every triangle is then counter-clockwise seen from outside.
        (int Axis, float Sign, int U, int V)[] faces =
        [
            (0, +1, 1, 2), (0, -1, 2, 1),
            (1, +1, 2, 0), (1, -1, 0, 2),
            (2, +1, 0, 1), (2, -1, 1, 0),
        ];
        foreach (var (axis, sign, u, v) in faces)
        {
            float[] us = Steps(Get(half, u), radius, segments);
            float[] vs = Steps(Get(half, v), radius, segments);
            int first = vertices.Count;
            foreach (float b in vs)
            foreach (float a in us)
            {
                Vector3 onBox = default;
                Set(ref onBox, axis, sign * Get(half, axis));
                Set(ref onBox, u, a);
                Set(ref onBox, v, b);
                Vector3 inner = Vector3.Clamp(onBox, -core, core);
                Vector3 normal = Vector3.Normalize(onBox - inner);
                vertices.Add(new MeshVertex(inner + normal * radius, normal));
            }
            int columns = us.Length;
            for (int j = 0; j < vs.Length - 1; j++)
            for (int i = 0; i < columns - 1; i++)
            {
                int a = first + j * columns + i, b = a + 1, c = a + columns, d = c + 1;
                indices.AddRange([(ushort) a, (ushort) b, (ushort) d, (ushort) a, (ushort) d, (ushort) c]);
            }
        }
        if (vertices.Count > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(segments), segments, "Too many vertices");
        return new MeshData([.. vertices], [.. indices]);
    }

    // Grid lines along one axis of a face, from -half to +half: `segments` steps through each rounded band,
    // one across the flat middle. A face holds half of each edge's rounding (the 45 degrees nearest to it),
    // and a point `d` into the band turns the normal by atan(d / radius), so the steps are spaced by that
    // angle to make the rounding even.
    private static float[] Steps(float half, float radius, int segments)
    {
        var steps = new List<float>();
        for (int k = segments; k >= 0; k--) steps.Add(-half + radius - radius * Tan(k, segments));
        for (int k = 0; k <= segments; k++) steps.Add(half - radius + radius * Tan(k, segments));
        // The two bands meet when the face is no wider than the rounding: drop the doubled point.
        if (MathF.Abs(steps[segments] - steps[segments + 1]) < 1e-6f) steps.RemoveAt(segments + 1);
        return [.. steps];

        static float Tan(int k, int segments) => MathF.Tan(MathF.PI / 4 * k / segments);
    }

    private static float Get(Vector3 v, int axis) => axis switch { 0 => v.X, 1 => v.Y, _ => v.Z };

    private static void Set(ref Vector3 v, int axis, float value)
    {
        if (axis == 0) v.X = value;
        else if (axis == 1) v.Y = value;
        else v.Z = value;
    }
}
