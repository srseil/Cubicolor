using System;
using System.Buffers.Binary;
using System.IO;

namespace Cubicolor.Audio;

// The "Quite OK Audio" format (qoaformat.org, Dominic Szablewski, MIT): lossy, 3.2 bits a sample, decoded with
// a few integer operations per sample. The game's music is stored in it; this is the format's encoder and
// decoder, written from its specification.
//
// A file is "qoaf", the number of samples per channel (32-bit, big-endian), then frames. A frame holds up to
// 256 slices of 20 samples per channel: an 8-byte header (channels, sample rate, samples per channel, frame
// size), each channel's predictor state (four history samples and four weights, 16-bit each), then the
// slices, channels interleaved. A slice is 64 bits: a 4-bit scale factor and twenty 3-bit residuals. Each
// sample is predicted by a sign-sign LMS filter over the last four, and the residual is quantised.
public static class Qoa
{
    public const int SliceLength = 20;
    public const int SlicesPerFrame = 256;
    public const int FrameLength = SliceLength * SlicesPerFrame;

    private static readonly int[] QuantTable = [7, 7, 7, 5, 5, 3, 3, 1, 0, 0, 2, 2, 4, 4, 6, 6, 6];   // -8..8
    private static readonly int[] ScaleFactors = BuildScaleFactors();
    private static readonly int[] Reciprocals = BuildReciprocals();
    private static readonly int[,] Dequant = BuildDequant();

    private static int[] BuildScaleFactors()
    {
        var table = new int[16];
        for (int s = 0; s < 16; s++) table[s] = (int) Math.Round(Math.Pow(s + 1, 2.75));
        return table;
    }

    private static int[] BuildReciprocals()
    {
        var table = new int[16];
        for (int s = 0; s < 16; s++) table[s] = ((1 << 16) + ScaleFactors[s] - 1) / ScaleFactors[s];
        return table;
    }

    private static int[,] BuildDequant()
    {
        double[] steps = [0.75, -0.75, 2.5, -2.5, 4.5, -4.5, 7, -7];
        var table = new int[16, 8];
        for (int s = 0; s < 16; s++)
            for (int q = 0; q < 8; q++)
            {
                double x = ScaleFactors[s] * steps[q];
                table[s, q] = x < 0 ? -(int) Math.Round(-x, MidpointRounding.AwayFromZero) : (int) Math.Round(x, MidpointRounding.AwayFromZero);
            }
        return table;
    }

    private struct Lms
    {
        public int H0, H1, H2, H3, W0, W1, W2, W3;

        public readonly int Predict() => (H0 * W0 + H1 * W1 + H2 * W2 + H3 * W3) >> 13;

        public void Update(int sample, int residual)
        {
            int delta = residual >> 4;
            W0 += H0 < 0 ? -delta : delta;
            W1 += H1 < 0 ? -delta : delta;
            W2 += H2 < 0 ? -delta : delta;
            W3 += H3 < 0 ? -delta : delta;
            (H0, H1, H2, H3) = (H1, H2, H3, sample);
        }
    }

    private static int Clamp16(int v) => v < short.MinValue ? short.MinValue : v > short.MaxValue ? short.MaxValue : v;

    // The division the encoder rounds residuals with: by a scale factor through its reciprocal, rounded away
    // from zero.
    private static int Divide(int v, int scaleFactor)
    {
        long reciprocal = Reciprocals[scaleFactor];
        int n = (int) ((v * reciprocal + (1 << 15)) >> 16);
        n += Math.Sign(v) - Math.Sign(n);
        return n;
    }

    // --- Encoding ---

    public static byte[] Encode(ReadOnlySpan<short> interleaved, int channels, int sampleRate)
    {
        if (channels is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(channels));
        int samples = interleaved.Length / channels;
        using var stream = new MemoryStream();
        Span<byte> word = stackalloc byte[8];
        stream.Write("qoaf"u8);
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint) samples);
        stream.Write(word[..4]);

        var lms = new Lms[channels];
        for (int c = 0; c < channels; c++) lms[c] = new Lms { W2 = -(1 << 13), W3 = 1 << 14 };
        var previousScale = new int[channels];

        for (int start = 0; start < samples; start += FrameLength)
        {
            int length = Math.Min(FrameLength, samples - start);
            int slices = (length + SliceLength - 1) / SliceLength;
            int size = 8 + 16 * channels + 8 * slices * channels;
            ulong header = ((ulong) (uint) channels << 56) | ((ulong) (uint) sampleRate << 32) | ((ulong) (uint) length << 16) | (uint) size;
            Write(stream, header);
            for (int c = 0; c < channels; c++)
            {
                Lms l = lms[c];
                Write(stream, Pack16(l.H0, l.H1, l.H2, l.H3));
                Write(stream, Pack16(l.W0, l.W1, l.W2, l.W3));
            }
            for (int slice = 0; slice < length; slice += SliceLength)
            {
                int sliceLength = Math.Min(SliceLength, length - slice);
                for (int c = 0; c < channels; c++)
                {
                    // Every scale factor is tried, starting from the last one chosen; the one with the least
                    // squared error wins.
                    ulong best = 0;
                    long bestError = long.MaxValue;
                    Lms bestLms = default;
                    int bestScale = 0;
                    for (int t = 0; t < 16; t++)
                    {
                        int scale = (t + previousScale[c]) % 16;
                        Lms trial = lms[c];
                        ulong bits = (ulong) scale;
                        long error = 0;
                        for (int i = 0; i < sliceLength; i++)
                        {
                            int sample = interleaved[(start + slice + i) * channels + c];
                            int predicted = trial.Predict();
                            int residual = sample - predicted;
                            int scaled = Divide(residual, scale);
                            int quantized = QuantTable[Math.Clamp(scaled + 8, 0, 16)];
                            int dequantized = Dequant[scale, quantized];
                            int reconstructed = Clamp16(predicted + dequantized);
                            long diff = sample - reconstructed;
                            error += diff * diff;
                            if (error >= bestError) break;
                            trial.Update(reconstructed, dequantized);
                            bits = (bits << 3) | (uint) quantized;
                        }
                        // Large weights make the predictor ring; the reference encoder steers away from them.
                        long power = ((long) trial.W0 * trial.W0 + (long) trial.W1 * trial.W1 + (long) trial.W2 * trial.W2 + (long) trial.W3 * trial.W3 >> 18) - 0x8ff;
                        if (power > 0) error += power * power;
                        if (error < bestError)
                        {
                            bestError = error;
                            best = bits;
                            bestLms = trial;
                            bestScale = scale;
                        }
                    }
                    previousScale[c] = bestScale;
                    lms[c] = bestLms;
                    // A short last slice is padded with zero residuals.
                    best <<= (SliceLength - sliceLength) * 3;
                    Write(stream, best);
                }
            }
        }
        return stream.ToArray();
    }

    private static ulong Pack16(int a, int b, int c, int d) =>
        ((ulong) (ushort) (short) a << 48) | ((ulong) (ushort) (short) b << 32) | ((ulong) (ushort) (short) c << 16) | (ushort) (short) d;

    private static void Write(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }

    // --- Decoding ---

    // Decodes a QOA file frame by frame, so the music is never held decoded in memory.
    // Reads a QOA stream a frame at a time, so a long soundtrack never sits in memory whole. The decoder owns the
    // stream; it must be able to seek, for Rewind.
    public sealed class Decoder : IDisposable
    {
        private readonly Stream _stream;
        private readonly byte[] _bytes;       // the current frame as read, header included
        private int _position, _end;          // within _bytes
        private readonly Lms[] _lms;
        private short[] _frame = [];
        private int _frameFrames, _frameRead;

        public int Channels { get; }
        public int SampleRate { get; }
        public int TotalFrames { get; }

        public Decoder(byte[] data) : this(new MemoryStream(data, writable: false)) { }

        public Decoder(Stream stream)
        {
            _stream = stream;
            Span<byte> head = stackalloc byte[16];
            if (stream.ReadAtLeast(head, 16, throwOnEndOfStream: false) < 16 || head[0] != 'q' || head[1] != 'o' || head[2] != 'a' || head[3] != 'f')
                throw new InvalidDataException("Not a QOA file");
            TotalFrames = (int) BinaryPrimitives.ReadUInt32BigEndian(head[4..]);
            ulong header = BinaryPrimitives.ReadUInt64BigEndian(head[8..]);
            Channels = (int) (header >> 56);
            SampleRate = (int) ((header >> 32) & 0xffffff);
            if (Channels == 0 || SampleRate == 0) throw new InvalidDataException("A QOA file without channels or rate");
            _lms = new Lms[Channels];
            _bytes = new byte[8 + Channels * 16 + FrameLength / SliceLength * Channels * 8];
            Rewind();
        }

        public void Rewind()
        {
            _stream.Position = 8;
            _frameFrames = _frameRead = 0;
        }

        public void Dispose() => _stream.Dispose();

        // Fills `output` with interleaved samples; returns how many sample frames it wrote (fewer at the end).
        public int Read(Span<short> output)
        {
            int frames = output.Length / Channels, written = 0;
            while (written < frames)
            {
                if (_frameRead == _frameFrames && !DecodeFrame()) break;
                int n = Math.Min(frames - written, _frameFrames - _frameRead);
                _frame.AsSpan(_frameRead * Channels, n * Channels).CopyTo(output.Slice(written * Channels));
                _frameRead += n;
                written += n;
            }
            return written;
        }

        private ulong Next()
        {
            ulong value = BinaryPrimitives.ReadUInt64BigEndian(_bytes.AsSpan(_position));
            _position += 8;
            return value;
        }

        // The next frame's bytes into _bytes: its header says how many there are. False at the end, or where
        // the stream breaks off or stops making sense.
        private bool Fill()
        {
            if (_stream.ReadAtLeast(_bytes.AsSpan(0, 8), 8, throwOnEndOfStream: false) < 8) return false;
            int size = (int) (BinaryPrimitives.ReadUInt64BigEndian(_bytes) & 0xffff);
            if (size < 8 || size > _bytes.Length) return false;
            if (_stream.ReadAtLeast(_bytes.AsSpan(8, size - 8), size - 8, throwOnEndOfStream: false) < size - 8) return false;
            _position = 0;
            _end = size;
            return true;
        }

        private bool DecodeFrame()
        {
            if (!Fill()) return false;
            ulong header = Next();
            int channels = (int) (header >> 56);
            int length = (int) ((header >> 16) & 0xffff);
            if (channels != Channels || length == 0 || length > FrameLength
                || 8 + channels * 16 + (length + SliceLength - 1) / SliceLength * channels * 8 > _end) return false;
            for (int c = 0; c < channels; c++)
            {
                ulong history = Next(), weights = Next();
                _lms[c] = new Lms
                {
                    H0 = (short) (history >> 48), H1 = (short) (history >> 32), H2 = (short) (history >> 16), H3 = (short) history,
                    W0 = (short) (weights >> 48), W1 = (short) (weights >> 32), W2 = (short) (weights >> 16), W3 = (short) weights,
                };
            }
            if (_frame.Length < length * channels) _frame = new short[FrameLength * channels];
            for (int slice = 0; slice < length; slice += SliceLength)
                for (int c = 0; c < channels; c++)
                {
                    ulong bits = Next();
                    int scale = (int) (bits >> 60);
                    int end = Math.Min(slice + SliceLength, length);
                    ref Lms lms = ref _lms[c];
                    for (int i = slice; i < end; i++)
                    {
                        int predicted = lms.Predict();
                        int quantized = (int) ((bits >> 57) & 7);
                        int dequantized = Dequant[scale, quantized];
                        int reconstructed = Clamp16(predicted + dequantized);
                        _frame[i * channels + c] = (short) reconstructed;
                        lms.Update(reconstructed, dequantized);
                        bits <<= 3;
                    }
                }
            _frameFrames = length;
            _frameRead = 0;
            return true;
        }
    }
}
