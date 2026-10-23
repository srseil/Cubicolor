using System;
using System.Numerics;

namespace Cubicolor.Render;

// Room in the overlay's atlas, handed out in shelves: each image goes on the first shelf tall enough with
// space left, or opens a new one. The first texels are kept fully covered for solid shapes.
public sealed class OverlayAtlas
{
    public const int Size = 1024;
    private const int Padding = 1;

    private readonly IOverlayAtlas _target;
    private int _shelfY, _shelfHeight, _x;

    public OverlayAtlas(IOverlayAtlas target)
    {
        if (target.Size != Size) throw new ArgumentException($"The atlas is {target.Size} texels, expected {Size}");
        _target = target;
        Span<byte> solid = stackalloc byte[4 * 4];
        solid.Fill(255);
        _target.Upload(0, 0, 4, 4, solid);
        _x = 4 + Padding;
        _shelfHeight = 4;
    }

    // A texel in the middle of the covered block.
    public static Vector2 Solid => new(2f / Size, 2f / Size);

    // Place an image of coverage; returns its texture coordinates, top left and bottom right.
    public (Vector2 TopLeft, Vector2 BottomRight) Add(int width, int height, ReadOnlySpan<byte> coverage)
    {
        if (width + Padding > Size || height + Padding > Size) throw new ArgumentException($"A {width}x{height} image does not fit the atlas");
        if (_x + width + Padding > Size)
        {
            _shelfY += _shelfHeight + Padding;
            _shelfHeight = 0;
            _x = 0;
        }
        if (_shelfY + height + Padding > Size) throw new InvalidOperationException("The overlay atlas is full");
        _target.Upload(_x, _shelfY, width, height, coverage);
        var topLeft = new Vector2(_x, _shelfY) / Size;
        var bottomRight = new Vector2(_x + width, _shelfY + height) / Size;
        _x += width + Padding;
        _shelfHeight = Math.Max(_shelfHeight, height);
        return (topLeft, bottomRight);
    }
}
