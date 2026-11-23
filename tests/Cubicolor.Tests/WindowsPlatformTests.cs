using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Cubicolor.Platform.D3D11;
using Cubicolor.Platform.Windows;
using Cubicolor.Render;
using Cubicolor.Simulation.Input;
using Xunit;

namespace Cubicolor.Tests;

// The Windows platform pieces that can be checked without a window or a controller.
public sealed class WindowsPlatformTests
{
    [Fact]
    public void Every_key_with_a_scan_code_maps_back_to_itself()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        foreach (Key key in Enum.GetValues<Key>())
            if (ScanCodes.ToCode(key) is var (code, extended))
                Assert.Equal(key, ScanCodes.ToKey(code, extended, 0));
    }

    [Fact]
    public void The_arrows_and_the_game_keys_have_scan_codes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        Assert.Equal(Key.Up, ScanCodes.ToKey(0x48, true, 0));
        Assert.Equal(Key.Kp8, ScanCodes.ToKey(0x48, false, 0));
        Assert.Equal(Key.RCtrl, ScanCodes.ToKey(0x1D, true, 0));
        Assert.Equal(Key.Pause, ScanCodes.ToKey(0x45, false, 0x13));
        foreach (Key key in new[] { Key.Up, Key.Down, Key.Left, Key.Right, Key.R, Key.Escape, Key.Return, Key.Space, Key.Backspace })
            Assert.NotNull(ScanCodes.ToCode(key));
    }

    [Fact]
    public void A_printing_key_is_named_by_the_layout_and_the_others_in_words()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        // Q is Q on every Latin layout (A on French AZERTY, so the letter is not checked exactly).
        Assert.Matches("^[A-Z]$", ScanCodes.Name(Key.Q));
        Assert.Equal("Enter", ScanCodes.Name(Key.Return));
        Assert.Equal("Left Shift", ScanCodes.Name(Key.LShift));
        Assert.Equal("F5", ScanCodes.Name(Key.F5));
    }

    [Fact]
    public void DirectWrite_reads_metrics_glyphs_kerning_and_coverage()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var font = new DirectWriteFont(Repo.Assets(Path.Combine("fonts", "Vollkorn-Regular.ttf")), 48);

        Assert.InRange(font.Ascent, 30, 60);
        Assert.InRange(font.Descent, 5, 25);
        ushort a = font.GlyphFor('A');
        Assert.NotEqual(0, a);
        Assert.Equal(0, font.GlyphFor(''));   // a private-use character Vollkorn hasn't
        Assert.InRange(font.Advance(a), 20, 48);
        // The font's own kerning, through a layout of the pair: A and V close up.
        Assert.True(font.Kerning('A', 'V') < -0.5f, $"A-V kerning {font.Kerning('A', 'V')}");
        Assert.Equal(0, font.Kerning('n', 'n'), 0.01f);

        GlyphInk? ink = font.Rasterize(a);
        Assert.NotNull(ink);
        Assert.InRange(ink.Height, 25, 48);
        // Its top sits about a cap height above the baseline, its foot on it.
        Assert.InRange(ink.Top, 25, 40);
        Assert.InRange(ink.Top - ink.Height, -2, 2);
        Assert.Contains(ink.Coverage, c => c == 255);
        Assert.Contains(ink.Coverage, c => c is > 0 and < 255);   // antialiased
        Assert.Null(font.Rasterize(font.GlyphFor(' ')));
    }

    [Fact]
    public void Direct3D_draws_a_frame_offscreen_and_reads_it_back()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        using var renderer = new D3D11Renderer(IntPtr.Zero, () => (64, 32), vsync: false);
        var mesh = renderer.Upload(RoundedBox.Build(new Vector3(1), 0.1f, 4));
        var scene = new Scene
        {
            Background = new Vector4(1, 0, 0, 1),
            Projection = Matrix4x4.CreateOrthographic(4, 2, -10, 10),
            Lighting = new Lighting(Vector3.UnitY, new Vector3(1), Vector3.Zero, 0, 1),
        };
        scene.Items.Add(new DrawItem(mesh, Matrix4x4.Identity, new Vector4(0, 0, 1, 1)));

        Assert.True(renderer.Draw(scene, [], []));
        byte[] pixels = renderer.ReadPixels();

        Assert.Equal(64 * 32 * 4, pixels.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels[..4]);                           // the background in a corner
        int middle = (16 * 64 + 32) * 4;
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixels[middle..(middle + 4)]);          // the box, lit by the ambient alone
        Assert.Equal(64, renderer.Width);
    }

    // The swap chain's path (create, resize, copy, present) on a window that is never shown.
    [Fact]
    public unsafe void Direct3D_presents_to_a_window()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        IntPtr window;
        fixed (char* staticClass = "STATIC")
            window = Win32.CreateWindowExW(0, staticClass, null, Win32.WS_OVERLAPPEDWINDOW, 0, 0, 200, 100, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, window);
        try
        {
            (int Width, int Height) size = (120, 80);
            using (var renderer = new D3D11Renderer(window, () => size, vsync: false))
            {
                var scene = new Scene { Background = new Vector4(0, 1, 0, 1) };
                Assert.True(renderer.Draw(scene, [], []));
                size = (90, 60);
                Assert.True(renderer.Draw(scene, [], []));
                Assert.Equal(new byte[] { 0, 255, 0, 255 }, renderer.ReadPixels()[..4]);
            }
            using (var renderer = new D3D11Renderer(window, () => size, vsync: true))
                Assert.True(renderer.Draw(new Scene(), [], []));
        }
        finally
        {
            Win32.DestroyWindow(window);
        }
    }

    // Plays (silence: the mixer has nothing to play) through the default device, when there is one.
    [Fact]
    public void WASAPI_takes_sound_from_the_mixer()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only");
        var mixer = new Audio.Mixer();
        using WasapiOutput? output = WasapiOutput.Start(mixer);
        Assert.SkipWhen(output == null, "No sound device");
        System.Threading.Thread.Sleep(200);
        output.Watch();
    }
}
