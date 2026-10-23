using System;
using System.Numerics;
using Cubicolor.Simulation.Levels;

namespace Cubicolor.Render;

// The game's colours, as both earlier versions defined them (sRGB hex), converted to the linear values the
// renderer works in.
public static class Palette
{
    public static readonly Vector4 Red = Hex(0xf2777a);
    public static readonly Vector4 Green = Hex(0x99cc99);
    public static readonly Vector4 Blue = Hex(0x6699cc);
    public static readonly Vector4 Yellow = Hex(0xffcc66);
    public static readonly Vector4 White = Hex(0xfcfaf6);
    public static readonly Vector4 Black = Hex(0x2d2d2d);
    public static readonly Vector4 Background = Hex(0xf2f0ed);
    public static readonly Vector4 DarkBackground = Hex(0x393939);
    public static readonly Vector4 Text = Hex(0x2d2d2d);
    public static readonly Vector4 DarkText = Hex(0xf2f0ed);

    public static Vector4 Of(Paint paint) => paint switch
    {
        Paint.Red => Red,
        Paint.Green => Green,
        Paint.Blue => Blue,
        Paint.Yellow => Yellow,
        _ => Black,
    };

    public static Vector4 Hex(uint rgb, float alpha = 1f) =>
        new(Linear((rgb >> 16) & 0xff), Linear((rgb >> 8) & 0xff), Linear(rgb & 0xff), alpha);

    public static Vector4 WithAlpha(this Vector4 color, float alpha) => color with { W = alpha };

    private static float Linear(uint channel)
    {
        float c = channel / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }
}
