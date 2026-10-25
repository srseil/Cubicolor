using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cubicolor.Audio;

namespace Cubicolor.Tools.SoundLab;

internal static class Program
{
    private const string Usage = """
        SoundLab measure <wav>...                  loudness over time, spectral centroid and peaks
        SoundLab encode-music <in.wav> <out.ogg> [quality]
                                                   the music, to Ogg Vorbis through oggenc (vorbis-tools), at
                                                   quality 5 unless given; then decoded again by the game's decoder
        SoundLab decode <in.ogg> <out.wav>         an Ogg Vorbis file through the game's decoder
        SoundLab render <dir>                      every synthesized effect as <dir>/<sound>.wav
        SoundLab modes <wav> [count]               the resonances of a struck sound: frequency, level, decay
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
            case "modes" when args.Length >= 2:
            {
                Wav.Clip clip = Wav.Read(args[1]);
                float[] mono = Analysis.Mono(clip.Samples, clip.Channels);
                int count = args.Length > 2 ? int.Parse(args[2]) : 8;
                foreach (var (hz, level, decay) in Analysis.Modes(mono, clip.SampleRate, count))
                    Console.WriteLine($"{hz,8:0} Hz  level {level,8:0.0}  decay {decay * 1000,7:0.0} ms");
                return 0;
            }
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
            case "encode-music" when args.Length is 3 or 4:
                return EncodeMusic(args[1], args[2], args.Length == 4 ? args[3] : "5");
            case "decode" when args.Length == 3:
            {
                using var decoder = new Vorbis.Decoder(File.OpenRead(args[1]));
                Wav.Write(args[2], new Wav.Clip(decoder.SampleRate, decoder.Channels, Wav.FromFloat(Decode(decoder))));
                Console.WriteLine($"{args[2]}: {decoder.TotalFrames} frames, {decoder.Channels} ch, {decoder.SampleRate} Hz");
                return 0;
            }
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

    // libvorbis is the encoder (only at build time: the game decodes with its own reader); the round trip's
    // signal-to-noise ratio is a check on the decoder more than a measure of quality, which is for the ear.
    private static int EncodeMusic(string input, string output, string quality)
    {
        var start = new ProcessStartInfo("oggenc", ["--quiet", "-q", quality, "-o", output, input]);
        using (Process? oggenc = Process.Start(start))
        {
            if (oggenc == null) throw new InvalidOperationException("oggenc did not start (brew install vorbis-tools)");
            oggenc.WaitForExit();
            if (oggenc.ExitCode != 0)
            {
                Console.Error.WriteLine($"oggenc failed with exit code {oggenc.ExitCode}");
                return 1;
            }
        }
        Wav.Clip clip = Wav.Read(input);
        using var decoder = new Vorbis.Decoder(File.OpenRead(output));
        float[] decoded = Decode(decoder);
        double signal = 0, noise = 0;
        for (int i = 0; i < Math.Min(decoded.Length, clip.Samples.Length); i++)
        {
            double original = clip.Samples[i] / 32768.0;
            signal += original * original;
            double d = original - decoded[i];
            noise += d * d;
        }
        long bytes = new FileInfo(output).Length;
        Console.WriteLine($"{clip.Seconds:0.0} s → {bytes / 1e6:0.0} MB ({bytes * 8 / clip.Seconds / 1000:0} kbit/s), " +
                          $"decoded {decoder.TotalFrames} of {clip.Frames} frames, SNR {10 * Math.Log10(signal / noise):0.0} dB");
        return 0;
    }

    private static float[] Decode(Vorbis.Decoder decoder)
    {
        var samples = new float[(long) decoder.TotalFrames * decoder.Channels];
        int frames = decoder.Read(samples);
        if (frames != decoder.TotalFrames) throw new InvalidDataException($"decoded {frames} of {decoder.TotalFrames} frames");
        return samples;
    }
}
