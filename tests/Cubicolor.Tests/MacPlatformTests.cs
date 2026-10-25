using System;
using System.Linq;
using Cubicolor.Platform.Mac;
using Cubicolor.Render;
using Xunit;

namespace Cubicolor.Tests;

// The macOS platform pieces that can be checked without a window or a controller.
public sealed class MacPlatformTests
{
    [Fact]
    public void A_controller_symbol_rasterizes_to_coverage()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS only");

        GlyphInk? ink = SymbolImages.Rasterize("a.circle", 32);

        Assert.NotNull(ink);
        Assert.InRange(ink.Height, 24, 48);
        Assert.Contains(ink.Coverage, c => c > 200);
        Assert.Contains(ink.Coverage, c => c == 0);
    }

    // The symbols the controls screen falls back to with no controller connected: each must exist.
    [Theory]
    [InlineData("a.circle"), InlineData("b.circle"), InlineData("x.circle"), InlineData("y.circle")]
    [InlineData("lb.rectangle.roundedbottom"), InlineData("rb.rectangle.roundedbottom")]
    [InlineData("line.3.horizontal.circle"), InlineData("rectangle.on.rectangle.circle")]
    [InlineData("dpad.up.filled"), InlineData("dpad.down.filled"), InlineData("dpad.left.filled"), InlineData("dpad.right.filled")]
    public void The_default_controller_symbols_exist(string name)
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS only");
        Assert.NotNull(SymbolImages.Rasterize(name, 32));
    }

    [Fact]
    public void An_unknown_symbol_is_none() =>
        Assert.Null(OperatingSystem.IsMacOS() ? SymbolImages.Rasterize("no.such.symbol.anywhere", 32) : null);
}
