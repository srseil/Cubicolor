using System;
using System.IO;
using System.Linq;
using Cubicolor.Audio;

namespace Cubicolor.Tools.SoundLab;

internal static class Program
{
    private const string Usage = """
        SoundLab measure <wav>...                  loudness over time, spectral centroid and peaks
        SoundLab encode-music <in.wav> <out.qoa>   the music, to QOA (with the round trip's signal-to-noise ratio)
        SoundLab render <dir>                      every synthesized effect as <dir>/<sound>.wav
        """;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine(Usage);
            return 2;
        }
        switch (args[0])
        {
            case "measure":
                foreach (string path in args.Skip(1)) Measure(path);
                return 0;
            case "render" when args.Length == 2:
                Directory.CreateDirectory(args[1]);
                foreach (Game.Sound sound in Enum.GetValues<Game.Sound>())
                {
                    float[] stereo = Synth.Make(sound);
                    string path = Path.Combine(args[1], $"{sound.ToString().ToLowerInvariant()}.wav");
                    Wav.Write(path, new Wav.Clip(Mixer.Rate, 2, Wav.FromFloat(stereo)));
                    Console.WriteLine(path);
                }
                return 0;
            case "encode-music" when args.Length == 3:
                EncodeMusic(args[1], args[2]);
                return 0;
            default:
                Console.WriteLine(Usage);
                return 2;
        }
    }

    private static void Measure(string path)
    {
        Wav.Clip clip = Wav.Read(path);
        float[] mono = Analysis.Mono(clip.Samples, clip.Channels);
        float peak = mono.Max(MathF.Abs);
        double rms = Math.Sqrt(mono.Average(s => (double) s * s));
        Console.WriteLine($"{Path.GetFileName(path)}: {clip.Seconds:0.000} s, {clip.Channels} ch, {clip.SampleRate} Hz, peak {peak:0.000}, rms {rms:0.000}");
        float[] envelope = Analysis.Envelope(mono, clip.SampleRate, 0.01);
        Console.WriteLine("  envelope (10 ms): " + string.Join(" ", envelope.Select(e => (e / peak).ToString("0.00"))));
        var (magnitude, binHz) = Analysis.Spectrum(mono, clip.SampleRate);
        Console.WriteLine($"  centroid {Analysis.Centroid(magnitude, binHz):0} Hz; peaks: " +
                          string.Join(", ", Analysis.Peaks(magnitude, binHz, 10).Select(p => $"{p.Hz:0}")));
        // The centroid and strongest peak in successive 50 ms windows: how the sound moves.
        int window = clip.SampleRate / 20;
        var moving = Enumerable.Range(0, mono.Length / window).Select(w =>
        {
            var (m, b) = Analysis.Spectrum(mono.AsSpan(w * window, window), clip.SampleRate);
            return Analysis.Peaks(m, b, 1) is [var top] ? $"{Analysis.Centroid(m, b):0}/{top.Hz:0}" : "-";
        });
        Console.WriteLine("  centroid/peak per 50 ms: " + string.Join(" ", moving));
    }

    private static void EncodeMusic(string input, string output)
    {
        Wav.Clip clip = Wav.Read(input);
        byte[] encoded = Qoa.Encode(clip.Samples, clip.Channels, clip.SampleRate);
        File.WriteAllBytes(output, encoded);
        var decoder = new Qoa.Decoder(encoded);
        var decoded = new short[clip.Samples.Length];
        int frames = decoder.Read(decoded);
        double signal = 0, noise = 0;
        for (int i = 0; i < frames * clip.Channels; i++)
        {
            signal += (double) clip.Samples[i] * clip.Samples[i];
            double d = clip.Samples[i] - decoded[i];
            noise += d * d;
        }
        Console.WriteLine($"{clip.Seconds:0.0} s → {encoded.Length / 1e6:0.0} MB ({encoded.Length * 8.0 / (clip.Frames * clip.Channels):0.00} bits/sample), " +
                          $"decoded {frames} of {clip.Frames} frames, SNR {10 * Math.Log10(signal / noise):0.0} dB");
    }
}
