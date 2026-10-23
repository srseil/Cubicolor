using System;
using Cubicolor.Audio;
using Cubicolor.Game;
using Xunit;

namespace Cubicolor.Tests;

public sealed class AudioTests
{
    [Fact]
    public void Qoa_round_trips_music_like_audio_with_little_noise()
    {
        // Two seconds of a chord with a slow swell, stereo, at 44.1 kHz.
        const int rate = 44100, frames = rate * 2;
        var samples = new short[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            double t = (double) i / rate;
            double swell = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 0.5 * t);
            double left = Math.Sin(2 * Math.PI * 220 * t) + 0.5 * Math.Sin(2 * Math.PI * 277 * t) + 0.3 * Math.Sin(2 * Math.PI * 330 * t);
            double right = Math.Sin(2 * Math.PI * 165 * t) + 0.4 * Math.Sin(2 * Math.PI * 440 * t);
            samples[2 * i] = (short) (left * swell * 9000);
            samples[2 * i + 1] = (short) (right * swell * 9000);
        }

        byte[] encoded = Qoa.Encode(samples, 2, rate);
        var decoder = new Qoa.Decoder(encoded);
        var decoded = new short[samples.Length];
        int read = decoder.Read(decoded);

        Assert.Equal((2, rate, frames), (decoder.Channels, decoder.SampleRate, decoder.TotalFrames));
        Assert.Equal(frames, read);
        Assert.InRange(encoded.Length * 8.0 / samples.Length, 3.1, 3.4);
        double signal = 0, noise = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            signal += (double) samples[i] * samples[i];
            noise += (double) (samples[i] - decoded[i]) * (samples[i] - decoded[i]);
        }
        Assert.True(10 * Math.Log10(signal / noise) > 30, $"SNR {10 * Math.Log10(signal / noise):0.0} dB");
    }

    [Fact]
    public void Rewinding_the_decoder_plays_the_same_samples_again()
    {
        var samples = new short[Qoa.FrameLength * 3];
        for (int i = 0; i < samples.Length; i++) samples[i] = (short) (Math.Sin(i * 0.01) * 10000);
        var decoder = new Qoa.Decoder(Qoa.Encode(samples, 1, 48000));
        var first = new short[1000];
        var again = new short[1000];

        decoder.Read(first);
        decoder.Read(new short[20000]);
        decoder.Rewind();
        decoder.Read(again);

        Assert.Equal(first, again);
    }

    [Theory]
    [InlineData(1.0, false, 1.0)]
    [InlineData(0.0, false, 0.0)]
    [InlineData(1.0, true, 0.0)]
    [InlineData(0.5, false, 0.1778)]   // -15 dB, the earlier versions' curve
    public void Volume_settings_follow_the_old_curve(double volume, bool muted, double gain) =>
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
