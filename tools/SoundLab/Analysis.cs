using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Cubicolor.Tools.SoundLab;

// What a sound is made of, measured: its loudness over time and where its energy sits in frequency.
internal static class Analysis
{
    public static float[] Mono(short[] samples, int channels)
    {
        var mono = new float[samples.Length / channels];
        for (int i = 0; i < mono.Length; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++) sum += samples[i * channels + c];
            mono[i] = sum / channels / 32768f;
        }
        return mono;
    }

    // RMS in windows of `seconds`.
    public static float[] Envelope(float[] mono, int rate, double seconds)
    {
        int window = (int) (rate * seconds);
        var result = new List<float>();
        for (int i = 0; i + window <= mono.Length; i += window)
        {
            double sum = 0;
            for (int j = 0; j < window; j++) sum += mono[i + j] * mono[i + j];
            result.Add((float) Math.Sqrt(sum / window));
        }
        return [.. result];
    }

    // Magnitude spectrum of a Hann-windowed stretch, zero-padded to a power of two.
    public static (float[] Magnitude, double BinHz) Spectrum(ReadOnlySpan<float> samples, int rate)
    {
        int n = 1;
        while (n < samples.Length) n <<= 1;
        var data = new Complex[n];
        for (int i = 0; i < samples.Length; i++)
            data[i] = samples[i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (samples.Length - 1)));
        Fft(data);
        var magnitude = new float[n / 2];
        for (int i = 0; i < magnitude.Length; i++) magnitude[i] = (float) data[i].Magnitude;
        return (magnitude, (double) rate / n);
    }

    public static double Centroid(float[] magnitude, double binHz)
    {
        double weighted = 0, total = 0;
        for (int i = 1; i < magnitude.Length; i++)
        {
            weighted += magnitude[i] * i * binHz;
            total += magnitude[i];
        }
        return total > 0 ? weighted / total : 0;
    }

    // The strongest local maxima, at least `apart` Hz from each other.
    public static List<(double Hz, float Level)> Peaks(float[] magnitude, double binHz, int count, double apart = 60)
    {
        var candidates = new List<(double, float)>();
        for (int i = 2; i < magnitude.Length - 2; i++)
            if (magnitude[i] > magnitude[i - 1] && magnitude[i] >= magnitude[i + 1])
                candidates.Add((i * binHz, magnitude[i]));
        var peaks = new List<(double Hz, float Level)>();
        foreach (var c in candidates.OrderByDescending(c => c.Item2))
        {
            if (peaks.Any(p => Math.Abs(p.Hz - c.Item1) < apart)) continue;
            peaks.Add(c);
            if (peaks.Count == count) break;
        }
        return peaks;
    }

    private static void Fft(Complex[] a)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int length = 2; length <= n; length <<= 1)
        {
            double angle = -2 * Math.PI / length;
            var w = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += length)
            {
                Complex wn = 1;
                for (int j = 0; j < length / 2; j++)
                {
                    Complex u = a[i + j], v = a[i + j + length / 2] * wn;
                    a[i + j] = u + v;
                    a[i + j + length / 2] = u - v;
                    wn *= w;
                }
            }
        }
    }

    // The resonances a struck sound is made of: its strongest spectral peaks just after the strike, each with
    // its starting level and how fast it dies away (the time constant of an exponential fitted to its
    // magnitude, frame by frame, while it stays well above the noise).
    public static List<(double Hz, double Level, double Decay)> Modes(float[] mono, int rate, int count)
    {
        const int window = 2048, hop = 128;
        int onset = 0;
        float peak = 0;
        for (int i = 0; i < mono.Length; i++)
            if (MathF.Abs(mono[i]) > peak) (peak, onset) = (MathF.Abs(mono[i]), i);
        int start = Math.Max(0, onset - window / 4);
        var (first, binHz) = Spectrum(mono.AsSpan(start, Math.Min(window, mono.Length - start)), rate);
        var modes = new List<(double, double, double)>();
        foreach (var (hz, level) in Peaks(first, binHz, count, apart: 40))
        {
            int bin = (int) Math.Round(hz / binHz);
            var times = new List<double>();
            var logs = new List<double>();
            for (int at = start; at + window <= mono.Length; at += hop)
            {
                var (m, _) = Spectrum(mono.AsSpan(at, window), rate);
                float magnitude = Math.Max(m[bin], Math.Max(m[Math.Max(0, bin - 1)], m[Math.Min(m.Length - 1, bin + 1)]));
                if (magnitude < level * 0.01) break;
                times.Add((double) (at - start) / rate);
                logs.Add(Math.Log(magnitude));
            }
            double decay = 0.005;
            if (times.Count >= 3)
            {
                double mt = times.Average(), ml = logs.Average();
                double slope = times.Zip(logs).Sum(p => (p.First - mt) * (p.Second - ml)) / times.Sum(t => (t - mt) * (t - mt));
                if (slope < 0) decay = -1 / slope;
            }
            modes.Add((hz, level, decay));
        }
        return modes;
    }
}
