using System;
using Cubicolor.Game;

namespace Cubicolor.Audio;

// The game's sound effects, made at startup instead of loaded. Each one imitates the recording the earlier
// versions used, measured with tools/SoundLab: the same length, loudness, envelope and the frequencies its
// energy sits at. Deterministic: the same samples every run.
public static class Synth
{
    private const int Rate = Mixer.Rate;

    public static float[] Make(Sound sound) => sound switch
    {
        Sound.Click => Stereo(Click()),
        Sound.Step => Stereo(Step()),
        Sound.Paint => Stereo(Paint()),
        // The original was the colouring sound played backwards.
        Sound.Unpaint => Stereo(Reversed(Paint())),
        Sound.Revive => Stereo(Revive()),
        Sound.Solve => Stereo(Solve()),
        _ => throw new ArgumentOutOfRangeException(nameof(sound), sound, null),
    };

    // A dry wooden click: the button click recording's resonances (frequency, starting amplitude, decay), as
    // SoundLab's modes measured them, under a short broadband tick. Decays this short come out about three times
    // too short from the measurement's 43 ms window, so they are set to match the recording's 10 ms envelope.
    private static float[] Click()
    {
        float[] s = Buffer(0.24);
        Modes(s, 0, [(938, 1.33, 0.0099), (1805, 0.96, 0.0081), (1688, 0.73, 0.0090), (1266, 0.6, 0.0096), (820, 0.44, 0.0123),
                     (680, 0.38, 0.0111), (469, 0.29, 0.0135), (3609, 0.43, 0.0090), (7242, 0.43, 0.0084)]);
        Noise(s, 0.6f, 0.006, new Random32(11));
        return Normalise(s, 0.26f);
    }

    // The knock of the cube landing: the step recording's resonances, low and gone within 20 ms (decays matched
    // to its envelope, as the click's).
    private static float[] Step()
    {
        float[] s = Buffer(0.5);
        Modes(s, 0, [(375, 2.2, 0.0072), (773, 0.75, 0.0072), (703, 0.6, 0.0075), (914, 0.36, 0.0084), (1242, 0.21, 0.0084),
                     (1148, 0.21, 0.0087), (1758, 0.21, 0.0072), (1406, 0.17, 0.0087)]);
        Noise(s, 0.35f, 0.002, new Random32(23));
        return Normalise(s, 0.25f);
    }

    // Water poured into a glass: bubbles at random moments, their pitch rising as the glass fills (560 to
    // 930 Hz over the sound), over a faint hiss.
    private static float[] Paint()
    {
        const double length = 0.763;
        float[] s = Buffer(length);
        var random = new Random32(7);
        int bubbles = 60;
        for (int i = 0; i < bubbles; i++)
        {
            double at = random.Next() * (length - 0.08);
            double fill = at / length;
            double pitch = (560 + 370 * fill) * (0.8 + 0.45 * random.Next());
            double duration = 0.015 + 0.03 * random.Next();
            float amplitude = (float) (random.Next() < 0.2 ? 0.8 + 0.2 * random.Next() : 0.15 + 0.35 * random.Next());
            Bubble(s, at, pitch, duration, amplitude);
        }
        BandNoise(s, 800, 0.03f, new Random32(5));
        return Normalise(s, 0.25f);
    }

    // A tile clicking into place: two sticks struck together, as in the earlier versions' recording. A bright
    // strike at 984 Hz that is gone in a few milliseconds, over a hollow 188 Hz body that rings a little longer;
    // the modes are the recording's, measured with SoundLab, under the faint tail of the room it was made in.
    private static float[] Revive()
    {
        float[] s = Buffer(0.431);
        const double at = 0.01;
        Modes(s, at, [(984, 1.9, 0.0068), (188, 0.12, 0.05), (469, 0.38, 0.0185), (1641, 0.62, 0.0079), (750, 0.31, 0.0151),
                      (1195, 0.38, 0.0091), (633, 0.18, 0.0174), (867, 0.19, 0.0155), (1266, 0.2, 0.0124)]);
        Offset(s, at, t => Noise(t, 0.5f, 0.0012, new Random32(37)));
        // The room the recording was made in: a faint noisy tail.
        Offset(s, at, t => Noise(t, 0.03f, 0.08, new Random32(41)));
        return Normalise(s, 0.2f);
    }

    // A small bell: inharmonic partials at 2236 and 5746 Hz, the higher one fading first. The strike dies
    // down fast (half in 70 ms) and leaves a long, quiet ring.
    private static float[] Solve()
    {
        float[] s = Buffer(1.603);
        Partial(s, 2236, 0.8f, 0.1);
        Partial(s, 2236, 0.22f, 0.6);
        Partial(s, 5746, 0.75f, 0.08);
        Partial(s, 10252, 0.15f, 0.07);
        Noise(s, 0.1f, 0.003, new Random32(3));
        Noise(s, 0.025f, 0.5, new Random32(4));
        return Normalise(s, 0.16f);
    }

    // --- Building blocks ---

    // Struck resonances from `at` seconds on: each a sine at its frequency, starting at its amplitude (with a
    // fixed phase of its own) and decaying with its time constant.
    private static void Modes(float[] s, double at, (double Hz, double Amplitude, double Decay)[] modes)
    {
        int start = (int) (at * Rate);
        var phases = new Random32(97);
        foreach (var (hz, amplitude, decay) in modes)
        {
            double phase = phases.Next() * 2 * Math.PI;
            for (int i = start; i < s.Length; i++)
            {
                double t = (double) (i - start) / Rate;
                double envelope = Math.Exp(-t / decay);
                if (envelope < 1e-4) break;
                s[i] += (float) (amplitude * envelope * Attack(t) * Math.Sin(2 * Math.PI * hz * t + phase));
            }
        }
    }

    private static float[] Buffer(double seconds) => new float[(int) (seconds * Rate)];

    // A sine at `hz` decaying exponentially with time constant `decay`, struck at the start.
    private static void Partial(float[] s, double hz, float amplitude, double decay)
    {
        for (int i = 0; i < s.Length; i++)
        {
            double t = (double) i / Rate;
            s[i] += amplitude * (float) (Math.Sin(2 * Math.PI * hz * t) * Math.Exp(-t / decay) * Attack(t));
        }
    }

    // One bubble: a short sine whose pitch rises as it closes, as a bubble's does.
    private static void Bubble(float[] s, double at, double hz, double duration, float amplitude)
    {
        int start = (int) (at * Rate), length = (int) (duration * Rate);
        double phase = 0;
        for (int i = 0; i < length && start + i < s.Length; i++)
        {
            double t = (double) i / Rate;
            double rise = 1 + 0.2 * t / duration;
            phase += 2 * Math.PI * hz * rise / Rate;
            double envelope = Math.Min(1, t / 0.002) * Math.Exp(-t / (duration / 3));
            s[start + i] += amplitude * (float) (Math.Sin(phase) * envelope);
        }
    }

    // White noise decaying with `decay`: the strike of a click or knock.
    private static void Noise(float[] s, float amplitude, double decay, Random32 random)
    {
        for (int i = 0; i < s.Length; i++)
        {
            double t = (double) i / Rate;
            s[i] += amplitude * (float) ((random.Next() * 2 - 1) * Math.Exp(-t / decay));
        }
    }

    // Noise through a resonant band-pass at `hz`, steady or decaying.
    private static void BandNoise(float[] s, double hz, float amplitude, Random32 random, double decay = double.PositiveInfinity)
    {
        // A two-pole resonator (RBJ band-pass, Q 2).
        double w = 2 * Math.PI * hz / Rate, alpha = Math.Sin(w) / (2 * 2.0);
        double b0 = alpha, a0 = 1 + alpha, a1 = -2 * Math.Cos(w), a2 = 1 - alpha;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            double t = (double) i / Rate;
            double x = random.Next() * 2 - 1;
            double y = (b0 * x - b0 * x2 - a1 * y1 - a2 * y2) / a0;
            (x2, x1, y2, y1) = (x1, x, y1, y);
            s[i] += amplitude * (float) (y * 4 * Math.Exp(-t / decay));
        }
    }

    // Applies `make` to the buffer as if it started `seconds` later.
    private static void Offset(float[] s, double seconds, Action<float[]> make)
    {
        int start = (int) (seconds * Rate);
        float[] part = new float[s.Length - start];
        make(part);
        for (int i = 0; i < part.Length; i++) s[start + i] += part[i];
    }

    // One millisecond of fade-in, so nothing starts with a click of its own.
    private static double Attack(double t) => Math.Min(1, t / 0.001);

    private static float[] Normalise(float[] s, float peak)
    {
        float max = 0;
        foreach (float x in s) max = MathF.Max(max, MathF.Abs(x));
        if (max > 0)
            for (int i = 0; i < s.Length; i++) s[i] *= peak / max;
        // Half a millisecond of fade-out at the very end.
        int fade = Rate / 2000;
        for (int i = 0; i < fade && i < s.Length; i++) s[s.Length - 1 - i] *= (float) i / fade;
        return s;
    }

    private static float[] Reversed(float[] s)
    {
        Array.Reverse(s);
        return s;
    }

    private static float[] Stereo(float[] mono)
    {
        var stereo = new float[mono.Length * 2];
        for (int i = 0; i < mono.Length; i++) stereo[2 * i] = stereo[2 * i + 1] = mono[i];
        return stereo;
    }

    // A small, fixed pseudo-random generator (xorshift), so every run makes the same sounds.
    private sealed class Random32(uint seed)
    {
        private uint _state = seed * 2654435761u + 1;

        public double Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state / 4294967296.0;
        }
    }
}
