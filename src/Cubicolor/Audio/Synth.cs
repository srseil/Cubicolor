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

    // A dry wooden click: a few resonances between 0.8 and 1.8 kHz, gone within 20 ms.
    private static float[] Click()
    {
        float[] s = Buffer(0.24);
        Partial(s, 929, 1.0f, 0.006);
        Partial(s, 1813, 0.6f, 0.004);
        Partial(s, 1271, 0.5f, 0.005);
        Partial(s, 1667, 0.3f, 0.004);
        Noise(s, 1.0f, 0.004, new Random32(11));
        return Normalise(s, 0.26f);
    }

    // A knock as the cube lands: a low tone dropping from 700 to 340 Hz in a few milliseconds.
    private static float[] Step()
    {
        float[] s = Buffer(0.5);
        Glide(s, 700, 340, 0.012, 1.0f, 0.008);
        Glide(s, 1400, 700, 0.010, 0.25f, 0.004);
        Noise(s, 0.6f, 0.003, new Random32(23));
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

    // A soft pop as a tile rises: a tap, a tone falling from 1 kHz to 180 Hz and a low ring, with a breath of
    // noise after it.
    private static float[] Revive()
    {
        float[] s = Buffer(0.431);
        Offset(s, 0.008, t => Noise(t, 0.5f, 0.002, new Random32(37)));
        Offset(s, 0.008, t => Glide(t, 1000, 180, 0.03, 1.0f, 0.012));
        Offset(s, 0.008, t => Partial(t, 190, 0.12f, 0.035));
        Offset(s, 0.008, t => BandNoise(t, 600, 0.04f, new Random32(31), decay: 0.05));
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

    // A sine sliding exponentially from `from` to `to` Hz over `slide` seconds, decaying with `decay`.
    private static void Glide(float[] s, double from, double to, double slide, float amplitude, double decay)
    {
        double phase = 0;
        for (int i = 0; i < s.Length; i++)
        {
            double t = (double) i / Rate;
            double hz = to + (from - to) * Math.Exp(-t / (slide / 3));
            phase += 2 * Math.PI * hz / Rate;
            s[i] += amplitude * (float) (Math.Sin(phase) * Math.Exp(-t / decay) * Attack(t));
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
