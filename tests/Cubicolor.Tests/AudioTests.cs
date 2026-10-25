using System;
using System.IO;
using Cubicolor.Audio;
using Cubicolor.Game;
using Xunit;

namespace Cubicolor.Tests;

public sealed class AudioTests
{
    // The fixtures are this signal through oggenc (stereo at 44.1 kHz, quality 3; mono at 22.05 kHz, quality 0):
    // a chord with a slow swell, and two clicks that make the encoder switch to short blocks.
    private static double Signal(double t, int channel)
    {
        double swell = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 0.5 * t);
        double v = channel == 0
            ? Math.Sin(2 * Math.PI * 220 * t) + 0.5 * Math.Sin(2 * Math.PI * 277 * t) + 0.3 * Math.Sin(2 * Math.PI * 330 * t)
            : Math.Sin(2 * Math.PI * 165 * t) + 0.4 * Math.Sin(2 * Math.PI * 440 * t);
        v *= swell * 0.25;
        foreach (double click in (double[]) [0.7, 1.4])
        {
            double d = t - click;
            if (d is >= 0 and < 0.005) v += 0.5 * Math.Exp(-d * 1000) * Math.Sin(2 * Math.PI * 3000 * d);
        }
        return v;
    }

    [Theory]
    [InlineData("stereo.ogg", 2, 44100, 88200, 30)]
    [InlineData("mono.ogg", 1, 22050, 22050, 25)]
    public void Vorbis_decodes_to_the_signal_that_was_encoded(string file, int channels, int rate, int frames, double minimumSnr)
    {
        using var decoder = new Vorbis.Decoder(File.OpenRead(Repo.Fixture(Path.Combine("audio", file))));
        var decoded = new float[(frames + 1000) * channels];

        int read = decoder.Read(decoded);

        Assert.Equal((channels, rate, frames), (decoder.Channels, decoder.SampleRate, decoder.TotalFrames));
        Assert.Equal(frames, read);
        double signal = 0, noise = 0;
        for (int i = 0; i < frames; i++)
            for (int c = 0; c < channels; c++)
            {
                double expected = Signal((double) i / rate, c);
                signal += expected * expected;
                noise += (expected - decoded[i * channels + c]) * (expected - decoded[i * channels + c]);
            }
        double snr = 10 * Math.Log10(signal / noise);
        Assert.True(snr > minimumSnr, $"SNR {snr:0.0} dB");
    }

    [Fact]
    public void Rewinding_the_decoder_plays_the_same_samples_again()
    {
        using var decoder = new Vorbis.Decoder(File.ReadAllBytes(Repo.Fixture(Path.Combine("audio", "stereo.ogg"))));
        var first = new float[3000];
        var again = new float[3000];

        decoder.Read(first);
        decoder.Read(new float[100000]);
        decoder.Rewind();
        decoder.Read(again);

        Assert.Equal(first, again);
    }

    [Fact]
    public void A_file_that_is_not_ogg_vorbis_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => new Vorbis.Decoder("RIFF....WAVEfmt nothing to see"u8.ToArray()));
        byte[] truncated = File.ReadAllBytes(Repo.Fixture(Path.Combine("audio", "stereo.ogg")))[..200];
        Assert.Throws<InvalidDataException>(() => new Vorbis.Decoder(truncated));
    }

    [Fact]
    public void The_mixer_plays_the_music_at_its_rate_and_loops_it()
    {
        var mixer = new Mixer();
        mixer.SetVolumes(1, 0);
        mixer.SetMusic(new Vorbis.Decoder(File.ReadAllBytes(Repo.Fixture(Path.Combine("audio", "stereo.ogg")))));
        // Three seconds of the two-second fixture, at the mixer's 48 kHz.
        var output = new float[Mixer.Rate * 3 * 2];

        mixer.Render(output);

        // The chord's loudness follows its swell, at the times the fixture has it, through the loop.
        foreach (double t in (double[]) [0.25, 0.5, 1.0, 1.2, 2.25, 2.5])
        {
            double expected = 0, played = 0;
            int at = (int) (t * Mixer.Rate), span = Mixer.Rate / 20;
            for (int i = at; i < at + span; i++)
            {
                double e = Signal((double) i / Mixer.Rate % 2.0, 0);
                expected += e * e;
                played += output[2 * i] * output[2 * i];
            }
            Assert.InRange(Math.Sqrt(played / expected), 0.9, 1.1);
        }
        mixer.SetMusic(null)?.Dispose();
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(2048)]
    public void The_fast_inverse_mdct_matches_its_definition(int n)
    {
        var random = new Random(n);
        var coefficients = new float[n / 2];
        for (int k = 0; k < coefficients.Length; k++) coefficients[k] = (float) (random.NextDouble() * 2 - 1);
        var output = new float[n];

        new Mdct(n).Inverse(coefficients, output);

        for (int i = 0; i < n; i++)
        {
            double expected = 0;
            for (int k = 0; k < n / 2; k++) expected += coefficients[k] * Math.Cos(2 * Math.PI / n * (i + 0.5 + n / 4.0) * (k + 0.5));
            Assert.True(Math.Abs(expected - output[i]) < 1e-3 * Math.Sqrt(n), $"y[{i}] = {output[i]}, expected {expected}");
        }
    }

    [Theory]
    [InlineData(1.0, false, 1.0)]
    [InlineData(0.0, false, 0.0)]
    [InlineData(1.0, true, 0.0)]
    [InlineData(0.5, false, 0.3163)]   // -10 dB: half as loud
    [InlineData(0.8, false, 0.6904)]   // -3.2 dB
    public void Volume_settings_are_linear_in_loudness(double volume, bool muted, double gain) =>
        Assert.Equal(gain, Mixer.Gain(volume, muted), 3);

    [Fact]
    public void Every_sound_is_made_the_same_every_time_and_stays_in_range()
    {
        foreach (Sound sound in Enum.GetValues<Sound>())
        {
            float[] a = Synth.Make(sound), b = Synth.Make(sound);
            Assert.Equal(a, b);
            Assert.All(a, s => Assert.InRange(s, -0.5f, 0.5f));
            Assert.True(a.Length > Mixer.Rate / 10, $"{sound} is too short");
        }
    }

    [Fact]
    public void The_mixer_plays_a_sound_to_its_end_and_then_silence()
    {
        var mixer = new Mixer();
        mixer.SetVolumes(0, 1);
        mixer.Play([0.25f, 0.25f, 0.5f, 0.5f]);
        var output = new float[8];

        mixer.Render(output);

        Assert.Equal([0.25f, 0.25f, 0.5f, 0.5f, 0, 0, 0, 0], output);
    }
}
