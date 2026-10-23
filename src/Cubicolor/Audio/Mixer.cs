using System;
using System.Collections.Generic;

namespace Cubicolor.Audio;

// The game's audio, mixed into interleaved stereo floats at the output's rate: any number of sound effects
// at once, and the music, streamed from QOA and looped. The platform output pulls from Render on its own
// thread; everything else is called from the game's, so the shared state sits behind a lock.
public sealed class Mixer
{
    public const int Rate = 48000;

    private sealed class Voice(float[] samples, float gain)
    {
        public readonly float[] Samples = samples;   // stereo, interleaved, at Rate
        public readonly float Gain = gain;
        public int Position;
    }

    private readonly object _gate = new();
    private readonly List<Voice> _voices = [];
    private Qoa.Decoder? _music;
    private short[] _musicBuffer = [];
    private double _musicPosition;        // in the music's own sample frames, fractional
    private int _musicBuffered, _musicBufferStart;
    private float _musicGain, _soundGain = 1;

    // The volume curve both earlier versions had: a setting v in 0..1 attenuates by 30 (1 - v) dB.
    public static float Gain(double volume, bool muted) => muted || volume <= 0 ? 0 : MathF.Pow(10, (float) ((1 - volume) * -30 / 20));

    public void SetVolumes(float music, float sound)
    {
        lock (_gate)
        {
            _musicGain = music;
            _soundGain = sound;
        }
    }

    public void Play(float[] stereo, float gain = 1)
    {
        lock (_gate) _voices.Add(new Voice(stereo, gain));
    }

    public void SetMusic(Qoa.Decoder? music)
    {
        lock (_gate)
        {
            _music = music;
            _musicPosition = 0;
            _musicBuffered = _musicBufferStart = 0;
            music?.Rewind();
        }
    }

    // Mix the next output.Length / 2 stereo frames.
    public void Render(Span<float> output)
    {
        output.Clear();
        lock (_gate)
        {
            if (_music != null && _musicGain > 0) MixMusic(output);
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                Voice voice = _voices[v];
                float gain = voice.Gain * _soundGain;
                int n = Math.Min(output.Length, voice.Samples.Length - voice.Position);
                for (int i = 0; i < n; i++) output[i] += voice.Samples[voice.Position + i] * gain;
                voice.Position += n;
                if (voice.Position >= voice.Samples.Length) _voices.RemoveAt(v);
            }
        }
        for (int i = 0; i < output.Length; i++) output[i] = SoftClip(output[i]);
    }

    // The music at its own rate (44.1 kHz) resampled linearly to the output's.
    private void MixMusic(Span<float> output)
    {
        Qoa.Decoder music = _music!;
        int channels = music.Channels;
        double step = (double) music.SampleRate / Rate;
        if (_musicBuffer.Length == 0) _musicBuffer = new short[Qoa.FrameLength * channels];
        for (int i = 0; i < output.Length; i += 2)
        {
            int index = (int) _musicPosition;
            float t = (float) (_musicPosition - index);
            float l0 = MusicSample(index, 0, channels), l1 = MusicSample(index + 1, 0, channels);
            float r0 = channels > 1 ? MusicSample(index, 1, channels) : l0, r1 = channels > 1 ? MusicSample(index + 1, 1, channels) : l1;
            output[i] += (l0 + (l1 - l0) * t) * _musicGain;
            output[i + 1] += (r0 + (r1 - r0) * t) * _musicGain;
            _musicPosition += step;
            if (_musicPosition >= music.TotalFrames)
            {
                // Loop: back to the start.
                _musicPosition -= music.TotalFrames;
                music.Rewind();
                _musicBuffered = _musicBufferStart = 0;
            }
        }
    }

    // A sample of the music by its frame index, decoding ahead as needed. Indices only grow (until the loop),
    // so the buffer keeps the current block and the frame just before it.
    private float MusicSample(int frame, int channel, int channels)
    {
        while (frame >= _musicBufferStart + _musicBuffered)
        {
            int keep = _musicBuffered > 0 ? 1 : 0;
            if (keep == 1)
                for (int c = 0; c < channels; c++) _musicBuffer[c] = _musicBuffer[(_musicBuffered - 1) * channels + c];
            int start = _musicBufferStart + _musicBuffered - keep;
            int read = _music!.Read(_musicBuffer.AsSpan(keep * channels));
            if (read == 0) return 0;
            _musicBufferStart = start;
            _musicBuffered = read + keep;
        }
        int at = frame - _musicBufferStart;
        return at < 0 ? 0 : _musicBuffer[at * channels + channel] / 32768f;
    }

    // Keeps a pile-up of sounds from clipping harshly: linear up to 0.8, then easing into ±1.
    private static float SoftClip(float x)
    {
        float a = MathF.Abs(x);
        if (a <= 0.8f) return x;
        return MathF.Sign(x) * (0.8f + 0.2f * MathF.Tanh((a - 0.8f) / 0.2f));
    }
}
