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

    [Fact]
    public void An_unknown_symbol_is_none() =>
        Assert.Null(OperatingSystem.IsMacOS() ? SymbolImages.Rasterize("no.such.symbol.anywhere", 32) : null);
}
