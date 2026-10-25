using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Cubicolor.Audio;

// Ogg Vorbis (the Vorbis I specification, xiph.org): the game's music is stored in it, at half the size of
// QOA and better to the ear. This is a decoder written from the specification, for what libvorbis has made
// since 2002: floor type 1 (type 0 is refused), residues 0 to 2, one logical stream.
//
// After three header packets (identification, comment, setup) every packet is one block of 2^k samples per
// channel. A block holds, per channel, a floor (a coarse spectral envelope as line segments in a dB scale)
// and a residue (the fine structure, vector-quantised through codebooks); their product is the spectrum, an
// inverse MDCT turns it into samples, and each block is windowed and overlapped with the one before.
public static class Vorbis
{
    // Reads an Ogg Vorbis stream a packet at a time, so a long soundtrack never sits in memory whole. The
    // decoder owns the stream; it must be able to seek, for Rewind.
    public sealed class Decoder : IDisposable
    {
        private readonly Stream _stream;
        private readonly OggReader _ogg;
        private readonly long _audioStart;            // the page the first audio packet starts on
        private readonly int[] _blockSizes = new int[2];
        private readonly Codebook[] _codebooks;
        private readonly Floor1[] _floors;
        private readonly Residue[] _residues;
        private readonly Mapping[] _mappings;
        private readonly Mode[] _modes;
        private readonly Mdct[] _mdcts;
        private readonly float[][] _windows;           // short; long by previous and next flags, as 1 + 2p + n
        private readonly float[][] _spectrum, _block, _previous;
        private readonly bool[] _floorUsed;
        private readonly int[] _floorY;
        private float[] _out = [];                     // decoded, not yet read, interleaved
        private int _outLength, _outRead;
        private int _previousSize;                     // 0 before the first block
        private long _position;                        // sample frames handed out so far

        public int Channels { get; }
        public int SampleRate { get; }
        public int TotalFrames { get; }

        public Decoder(byte[] data) : this(new MemoryStream(data, writable: false)) { }

        public Decoder(Stream stream)
        {
            _stream = stream;
            _ogg = new OggReader(stream);

            var bits = new BitReader();
            if (!_ogg.NextPacket(out byte[] packet, out int length) || !Header(ref bits, packet, length, 1))
                throw new InvalidDataException("Not an Ogg Vorbis stream");
            if (bits.Read(32) != 0) throw new InvalidDataException("Not Vorbis I");
            Channels = (int) bits.Read(8);
            SampleRate = (int) bits.Read(32);
            bits.Read(32);
            bits.Read(32);
            bits.Read(32);
            _blockSizes[0] = 1 << (int) bits.Read(4);
            _blockSizes[1] = 1 << (int) bits.Read(4);
            if (Channels == 0 || SampleRate <= 0 || _blockSizes[0] < 64 || _blockSizes[1] > 8192 || _blockSizes[0] > _blockSizes[1]
                || bits.Read(1) != 1 || bits.Ended)
                throw new InvalidDataException("A Vorbis identification header out of range");

            if (!_ogg.NextPacket(out packet, out length) || !Header(ref bits, packet, length, 3))
                throw new InvalidDataException("No Vorbis comment header");
            if (!_ogg.NextPacket(out packet, out length) || !Header(ref bits, packet, length, 5))
                throw new InvalidDataException("No Vorbis setup header");

            _codebooks = new Codebook[bits.Read(8) + 1];
            for (int i = 0; i < _codebooks.Length; i++) _codebooks[i] = new Codebook(ref bits);
            int transforms = (int) bits.Read(6) + 1;
            for (int i = 0; i < transforms; i++)
                if (bits.Read(16) != 0) throw new InvalidDataException("A Vorbis time-domain transform");
            _floors = new Floor1[bits.Read(6) + 1];
            for (int i = 0; i < _floors.Length; i++)
            {
                if (bits.Read(16) != 1) throw new InvalidDataException("Vorbis floor type 0 is not supported");
                _floors[i] = new Floor1(ref bits, _codebooks);
            }
            _residues = new Residue[bits.Read(6) + 1];
            for (int i = 0; i < _residues.Length; i++) _residues[i] = new Residue(ref bits, _codebooks);
            _mappings = new Mapping[bits.Read(6) + 1];
            for (int i = 0; i < _mappings.Length; i++) _mappings[i] = new Mapping(ref bits, Channels, _floors.Length, _residues.Length);
            _modes = new Mode[bits.Read(6) + 1];
            for (int i = 0; i < _modes.Length; i++)
            {
                bool longBlock = bits.Read(1) == 1;
                if (bits.Read(16) != 0 || bits.Read(16) != 0) throw new InvalidDataException("A Vorbis window or transform type other than 0");
                int mapping = (int) bits.Read(8);
                if (mapping >= _mappings.Length) throw new InvalidDataException("A Vorbis mode's mapping is out of range");
                _modes[i] = new Mode(longBlock, mapping);
            }
            if (bits.Read(1) != 1 || bits.Ended) throw new InvalidDataException("A Vorbis setup header broken off");
            if (!_ogg.AtPageEnd) throw new InvalidDataException("Vorbis audio does not start on a fresh page");
            _audioStart = _ogg.PagePosition;

            long total = OggReader.LastGranule(stream);
            if (total is <= 0 or > int.MaxValue) throw new InvalidDataException("A Vorbis stream without a length");
            TotalFrames = (int) total;

            _mdcts = [new Mdct(_blockSizes[0]), new Mdct(_blockSizes[1])];
            _windows = new float[5][];
            _windows[0] = Window(_blockSizes[0], _blockSizes[0], _blockSizes[0], _blockSizes[0]);
            for (int p = 0; p < 2; p++)
                for (int n = 0; n < 2; n++)
                    _windows[1 + 2 * p + n] = Window(_blockSizes[1], _blockSizes[p], _blockSizes[n], _blockSizes[0]);
            int maxBlock = _blockSizes[1];
            _spectrum = new float[Channels][];
            _block = new float[Channels][];
            _previous = new float[Channels][];
            for (int c = 0; c < Channels; c++)
            {
                _spectrum[c] = new float[maxBlock / 2];
                _block[c] = new float[maxBlock];
                _previous[c] = new float[maxBlock];
            }
            _floorUsed = new bool[Channels];
            int maxValues = 0;
            foreach (Floor1 floor in _floors) maxValues = Math.Max(maxValues, floor.X.Length);
            _floorY = new int[maxValues];
            _out = new float[maxBlock * Channels];
            Rewind();
        }

        public void Rewind()
        {
            _ogg.Seek(_audioStart);
            _previousSize = 0;
            _outLength = _outRead = 0;
            _position = 0;
        }

        public void Dispose() => _stream.Dispose();

        // Fills `output` with interleaved samples; returns how many sample frames it wrote (fewer at the end).
        public int Read(Span<float> output)
        {
            int frames = output.Length / Channels, written = 0;
            while (written < frames && _position < TotalFrames)
            {
                if (_outRead == _outLength && !DecodePacket()) break;
                int n = (int) Math.Min(Math.Min(frames - written, _outLength - _outRead), TotalFrames - _position);
                _out.AsSpan(_outRead * Channels, n * Channels).CopyTo(output[(written * Channels)..]);
                _outRead += n;
                written += n;
                _position += n;
            }
            return written;
        }

        private static bool Header(ref BitReader bits, byte[] packet, int length, int type)
        {
            bits = new BitReader(packet, length);
            if (bits.Read(8) != (uint) type) return false;
            foreach (byte b in "vorbis"u8)
                if (bits.Read(8) != b) return false;
            return true;
        }

        // The next audio packet into _out; packets that are not audio, or make no sense, are skipped.
        private bool DecodePacket()
        {
            while (_ogg.NextPacket(out byte[] packet, out int length))
            {
                if (length == 0) continue;
                var bits = new BitReader(packet, length);
                if (bits.Read(1) != 0) continue;
                if (Decode(ref bits)) return true;
            }
            return false;
        }

        private bool Decode(ref BitReader bits)
        {
            int modeNumber = (int) bits.Read(Ilog(_modes.Length - 1));
            if (modeNumber >= _modes.Length) return false;
            Mode mode = _modes[modeNumber];
            int n = _blockSizes[mode.LongBlock ? 1 : 0], half = n / 2;
            float[] window = _windows[0];
            if (mode.LongBlock)
            {
                int previous = (int) bits.Read(1), next = (int) bits.Read(1);
                window = _windows[1 + 2 * previous + next];
            }
            if (bits.Ended) return false;
            Mapping mapping = _mappings[mode.Mapping];

            for (int c = 0; c < Channels; c++)
            {
                Floor1 floor = _floors[mapping.SubmapFloor[mapping.Mux[c]]];
                _floorUsed[c] = floor.Decode(ref bits, _codebooks, _floorY, _spectrum[c], half);
            }
            // A coupled pair is decoded whole when either side has a floor.
            for (int s = 0; s < mapping.Magnitude.Length; s++)
            {
                int m = mapping.Magnitude[s], a = mapping.Angle[s];
                if (_floorUsed[m] || _floorUsed[a]) _floorUsed[m] = _floorUsed[a] = true;
            }

            Span<int> members = stackalloc int[Channels];
            for (int submap = 0; submap < mapping.SubmapFloor.Length; submap++)
            {
                int count = 0;
                for (int c = 0; c < Channels; c++)
                    if (mapping.Mux[c] == submap) members[count++] = c;
                _residues[mapping.SubmapResidue[submap]].Decode(ref bits, _codebooks, _block, members[..count], _floorUsed, half);
            }

            for (int s = mapping.Magnitude.Length - 1; s >= 0; s--)
            {
                float[] magnitude = _block[mapping.Magnitude[s]], angle = _block[mapping.Angle[s]];
                for (int i = 0; i < half; i++)
                {
                    float m = magnitude[i], a = angle[i];
                    if (m > 0)
                    {
                        if (a > 0) angle[i] = m - a;
                        else { angle[i] = m; magnitude[i] = m + a; }
                    }
                    else
                    {
                        if (a > 0) angle[i] = m + a;
                        else { angle[i] = m; magnitude[i] = m - a; }
                    }
                }
            }

            // The spectrum is floor times residue, then back to samples, windowed.
            Mdct mdct = _mdcts[mode.LongBlock ? 1 : 0];
            for (int c = 0; c < Channels; c++)
            {
                float[] spectrum = _spectrum[c], block = _block[c];
                if (_floorUsed[c])
                    for (int i = 0; i < half; i++) spectrum[i] *= block[i];
                else
                    Array.Clear(spectrum, 0, half);
                mdct.Inverse(spectrum.AsSpan(0, half), block.AsSpan(0, n));
                for (int i = 0; i < n; i++) block[i] *= window[i];
            }

            // What a block adds is from the centre of the one before to its own centre; the first gives nothing.
            int p = _previousSize;
            int frames = p == 0 ? 0 : p / 4 + n / 4;
            for (int c = 0; c < Channels; c++)
            {
                float[] previous = _previous[c], block = _block[c];
                for (int i = 0; i < frames; i++)
                {
                    int at = p / 2 + i, current = i - p / 4 + n / 4;
                    float v = (at < p ? previous[at] : 0) + (current >= 0 ? block[current] : 0);
                    _out[i * Channels + c] = v;
                }
                _previous[c] = block;
                _block[c] = previous;
            }
            _previousSize = n;
            _outLength = frames;
            _outRead = 0;
            return true;
        }

        // The window of a block of size n: rising over the overlap with the block before, falling over the one
        // with the block after; a long block next to a short one overlaps only the short one's width.
        private static float[] Window(int n, int previous, int next, int shortSize)
        {
            var window = new float[n];
            int leftN = Math.Min(previous, n) / 2, rightN = Math.Min(next, n) / 2;
            int leftStart = n / 4 - leftN / 2, rightStart = n * 3 / 4 - rightN / 2;
            if (previous == n || n == shortSize) leftStart = 0;
            if (next == n || n == shortSize) rightStart = n / 2;
            for (int i = 0; i < n; i++)
            {
                double w;
                if (i < leftStart) w = 0;
                else if (i < leftStart + leftN) w = Slope((i - leftStart + 0.5) / leftN);
                else if (i < rightStart) w = 1;
                else if (i < rightStart + rightN) w = Slope(1 - (i - rightStart + 0.5) / rightN);
                else w = 0;
                window[i] = (float) w;
            }
            return window;
        }

        private static double Slope(double x)
        {
            double s = Math.Sin(x * Math.PI / 2);
            return Math.Sin(Math.PI / 2 * s * s);
        }
    }

    private static int Ilog(int x) => x <= 0 ? 0 : 32 - BitOperations.LeadingZeroCount((uint) x);

    // Bits are packed least significant first. Reading past the end gives zeros and sets Ended, which the
    // audio decode takes as the specification's end-of-packet condition.
    private struct BitReader
    {
        private readonly byte[] _data;
        private readonly int _length;
        private int _byte, _bit;

        public BitReader(byte[] data, int length)
        {
            _data = data;
            _length = length;
        }

        public bool Ended { get; private set; }

        public uint Read(int count)
        {
            uint value = 0;
            for (int got = 0; got < count;)
            {
                if (_byte >= _length)
                {
                    Ended = true;
                    return 0;
                }
                int take = Math.Min(8 - _bit, count - got);
                uint bits = (uint) (_data[_byte] >> _bit) & ((1u << take) - 1);
                value |= bits << got;
                got += take;
                _bit += take;
                if (_bit == 8)
                {
                    _bit = 0;
                    _byte++;
                }
            }
            return value;
        }

        // Up to 32 bits without consuming them, zeros past the end; and how many of them are real.
        public readonly uint Peek(int count, out int available)
        {
            ulong value = 0;
            int got = 0, at = _byte, bit = _bit;
            while (got < count && at < _length)
            {
                value |= (ulong) (_data[at] >> bit) << got;
                got += 8 - bit;
                bit = 0;
                at++;
            }
            available = Math.Min(got, count);
            return (uint) (value & ((1ul << count) - 1));
        }

        public void Skip(int count)
        {
            int position = _byte * 8 + _bit + count;
            if (position > _length * 8)
            {
                Ended = true;
                position = _length * 8;
            }
            _byte = position >> 3;
            _bit = position & 7;
        }

        public static float Float32(uint x)
        {
            double mantissa = x & 0x1fffff;
            int exponent = (int) ((x & 0x7fe00000) >> 21);
            if ((x & 0x80000000) != 0) mantissa = -mantissa;
            return (float) Math.ScaleB(mantissa, exponent - 788);
        }
    }

    // A Huffman code over entries, each optionally a vector (its "VQ" lookup) of Dimensions floats.
    private sealed class Codebook
    {
        private const int FastBits = 10;

        public readonly int Dimensions, Entries;
        public float[]? Vectors;                    // Entries × Dimensions, when the book has a lookup
        private readonly int[] _fast;               // by the next FastBits bits: entry, or -1
        private readonly byte[] _fastLength;
        private readonly uint[] _slowCode = [];     // the longer codewords, bit-reversed (as read), and their lengths
        private readonly byte[] _slowLength = [];
        private readonly int[] _slowEntry = [];

        public Codebook(ref BitReader bits)
        {
            if (bits.Read(24) != 0x564342) throw new InvalidDataException("A Vorbis codebook without its sync pattern");
            Dimensions = (int) bits.Read(16);
            Entries = (int) bits.Read(24);
            if (Dimensions == 0 || Entries == 0) throw new InvalidDataException("An empty Vorbis codebook");
            var lengths = new byte[Entries];       // 0: unused
            if (bits.Read(1) == 1)
            {
                int entry = 0, length = (int) bits.Read(5) + 1;
                while (entry < Entries)
                {
                    int count = (int) bits.Read(Ilog(Entries - entry));
                    if (entry + count > Entries || length > 32) throw new InvalidDataException("A Vorbis codebook's lengths overrun it");
                    for (int i = 0; i < count; i++) lengths[entry + i] = (byte) length;
                    entry += count;
                    length++;
                }
            }
            else
            {
                bool sparse = bits.Read(1) == 1;
                for (int i = 0; i < Entries; i++)
                    if (!sparse || bits.Read(1) == 1) lengths[i] = (byte) (bits.Read(5) + 1);
            }

            // Codewords in entry order, each the first one free at its length (the specification's rule).
            var codes = new uint[Entries];
            Span<uint> available = stackalloc uint[33];
            bool first = true;
            for (int i = 0; i < Entries; i++)
            {
                int length = lengths[i];
                if (length == 0) continue;
                if (first)
                {
                    first = false;
                    codes[i] = 0;
                    for (int l = 1; l <= length; l++) available[l] = 1u << (32 - l);
                    continue;
                }
                int z = length;
                while (z > 0 && available[z] == 0) z--;
                if (z == 0) throw new InvalidDataException("A Vorbis codebook with too many codewords");
                uint code = available[z];
                available[z] = 0;
                codes[i] = code >> (32 - length);
                for (int l = length; l > z; l--) available[l] = code + (1u << (32 - l));
            }

            _fast = new int[1 << FastBits];
            _fastLength = new byte[1 << FastBits];
            Array.Fill(_fast, -1);
            var slowCode = new List<uint>();
            var slowLength = new List<byte>();
            var slowEntry = new List<int>();
            for (int i = 0; i < Entries; i++)
            {
                int length = lengths[i];
                if (length == 0) continue;
                uint reversed = Reverse(codes[i], length);
                if (length <= FastBits)
                    for (uint fill = reversed; fill < 1u << FastBits; fill += 1u << length)
                    {
                        _fast[fill] = i;
                        _fastLength[fill] = (byte) length;
                    }
                else
                {
                    slowCode.Add(reversed);
                    slowLength.Add((byte) length);
                    slowEntry.Add(i);
                }
            }
            _slowCode = [.. slowCode];
            _slowLength = [.. slowLength];
            _slowEntry = [.. slowEntry];

            int lookup = (int) bits.Read(4);
            if (lookup == 0) return;
            if (lookup > 2) throw new InvalidDataException("A Vorbis codebook lookup type above 2");
            float minimum = BitReader.Float32(bits.Read(32)), delta = BitReader.Float32(bits.Read(32));
            int valueBits = (int) bits.Read(4) + 1;
            bool sequence = bits.Read(1) == 1;
            int values = lookup == 1 ? Lookup1Values(Entries, Dimensions) : Entries * Dimensions;
            var multiplicands = new uint[values];
            for (int i = 0; i < values; i++) multiplicands[i] = bits.Read(valueBits);
            if (bits.Ended) throw new InvalidDataException("A Vorbis codebook broken off");
            Vectors = new float[Entries * Dimensions];
            for (int entry = 0; entry < Entries; entry++)
            {
                float last = 0;
                int divisor = 1;
                for (int d = 0; d < Dimensions; d++)
                {
                    int offset = lookup == 1 ? entry / divisor % values : entry * Dimensions + d;
                    float value = multiplicands[offset] * delta + minimum + last;
                    if (sequence) last = value;
                    Vectors[entry * Dimensions + d] = value;
                    if (lookup == 1) divisor *= values;
                }
            }
        }

        // The largest r with r^dimensions <= entries.
        private static int Lookup1Values(int entries, int dimensions)
        {
            int r = (int) Math.Floor(Math.Pow(entries, 1.0 / dimensions));
            while (Math.Pow(r + 1, dimensions) <= entries) r++;
            while (r > 0 && Math.Pow(r, dimensions) > entries) r--;
            return r;
        }

        private static uint Reverse(uint code, int length)
        {
            uint r = 0;
            for (int i = 0; i < length; i++) r |= ((code >> i) & 1) << (length - 1 - i);
            return r;
        }

        // The next entry, or -1 at the end of the packet (or on a codeword the book does not have).
        public int Decode(ref BitReader bits)
        {
            uint peek = bits.Peek(FastBits, out int available);
            int entry = _fast[peek];
            if (entry >= 0)
            {
                int length = _fastLength[peek];
                bits.Skip(length);
                return length > available ? -1 : entry;
            }
            uint wide = bits.Peek(32, out available);
            for (int i = 0; i < _slowCode.Length; i++)
            {
                int length = _slowLength[i];
                if ((wide & (uint) ((1ul << length) - 1)) != _slowCode[i]) continue;
                bits.Skip(length);
                return length > available ? -1 : _slowEntry[i];
            }
            bits.Skip(32);
            return -1;
        }
    }

    // Floor type 1: a piecewise-linear curve through points at fixed frequencies, their heights coded per
    // packet relative to a prediction from their neighbours.
    private sealed class Floor1
    {
        private static readonly int[] Ranges = [256, 128, 86, 64];
        private static readonly float[] InverseDb = BuildInverseDb();

        public readonly int[] X;
        private readonly int[] _partitionClass, _classDimensions, _classSubclasses, _classMasterbook;
        private readonly int[][] _subclassBooks;
        private readonly int _multiplier;
        private readonly int[] _low, _high, _sorted;   // per point: its neighbours below and above so far; X order
        private readonly bool[] _step2;
        private readonly int[] _finalY;

        public Floor1(ref BitReader bits, Codebook[] books)
        {
            int partitions = (int) bits.Read(5);
            _partitionClass = new int[partitions];
            int maxClass = -1;
            for (int p = 0; p < partitions; p++)
            {
                _partitionClass[p] = (int) bits.Read(4);
                maxClass = Math.Max(maxClass, _partitionClass[p]);
            }
            _classDimensions = new int[maxClass + 1];
            _classSubclasses = new int[maxClass + 1];
            _classMasterbook = new int[maxClass + 1];
            _subclassBooks = new int[maxClass + 1][];
            for (int c = 0; c <= maxClass; c++)
            {
                _classDimensions[c] = (int) bits.Read(3) + 1;
                _classSubclasses[c] = (int) bits.Read(2);
                if (_classSubclasses[c] > 0)
                {
                    _classMasterbook[c] = (int) bits.Read(8);
                    if (_classMasterbook[c] >= books.Length) throw new InvalidDataException("A Vorbis floor's book is out of range");
                }
                _subclassBooks[c] = new int[1 << _classSubclasses[c]];
                for (int j = 0; j < _subclassBooks[c].Length; j++)
                {
                    _subclassBooks[c][j] = (int) bits.Read(8) - 1;
                    if (_subclassBooks[c][j] >= books.Length) throw new InvalidDataException("A Vorbis floor's book is out of range");
                }
            }
            _multiplier = (int) bits.Read(2) + 1;
            int rangeBits = (int) bits.Read(4);
            var x = new List<int> { 0, 1 << rangeBits };
            foreach (int c in _partitionClass)
                for (int j = 0; j < _classDimensions[c]; j++) x.Add((int) bits.Read(rangeBits));
            X = [.. x];
            if (X.Length > 65 || new HashSet<int>(X).Count != X.Length) throw new InvalidDataException("A Vorbis floor's points repeat");

            _low = new int[X.Length];
            _high = new int[X.Length];
            for (int i = 2; i < X.Length; i++)
            {
                int low = 0, high = 1;
                for (int j = 0; j < i; j++)
                {
                    if (X[j] < X[i] && X[j] > X[low]) low = j;
                    if (X[j] > X[i] && X[j] < X[high]) high = j;
                }
                _low[i] = low;
                _high[i] = high;
            }
            _sorted = new int[X.Length];
            for (int i = 0; i < X.Length; i++) _sorted[i] = i;
            int[] xs = X;
            Array.Sort(_sorted, (a, b) => xs[a].CompareTo(xs[b]));
            _step2 = new bool[X.Length];
            _finalY = new int[X.Length];
        }

        // The curve over the first n bins into `curve`; false when the channel has no floor this packet.
        public bool Decode(ref BitReader bits, Codebook[] books, int[] y, float[] curve, int n)
        {
            if (bits.Read(1) == 0) return false;
            int range = Ranges[_multiplier - 1];
            int yBits = Ilog(range - 1);
            y[0] = (int) bits.Read(yBits);
            y[1] = (int) bits.Read(yBits);
            int offset = 2;
            foreach (int c in _partitionClass)
            {
                int dimensions = _classDimensions[c], subclassBits = _classSubclasses[c];
                int mask = (1 << subclassBits) - 1, value = 0;
                if (subclassBits > 0)
                {
                    value = books[_classMasterbook[c]].Decode(ref bits);
                    if (value < 0) return false;
                }
                for (int j = 0; j < dimensions; j++)
                {
                    int book = _subclassBooks[c][value & mask];
                    value >>= subclassBits;
                    if (book >= 0)
                    {
                        int v = books[book].Decode(ref bits);
                        if (v < 0) return false;
                        y[offset + j] = v;
                    }
                    else
                    {
                        y[offset + j] = 0;
                    }
                }
                offset += dimensions;
            }
            if (bits.Ended) return false;

            // Each point's height from its prediction and the coded difference.
            _step2[0] = _step2[1] = true;
            _finalY[0] = y[0];
            _finalY[1] = y[1];
            for (int i = 2; i < X.Length; i++)
            {
                int low = _low[i], high = _high[i];
                int predicted = RenderPoint(X[low], _finalY[low], X[high], _finalY[high], X[i]);
                int value = y[i];
                int highRoom = range - predicted, lowRoom = predicted;
                int room = Math.Min(highRoom, lowRoom) * 2;
                if (value != 0)
                {
                    _step2[low] = _step2[high] = _step2[i] = true;
                    if (value >= room)
                        _finalY[i] = highRoom > lowRoom ? value - lowRoom + predicted : predicted - value + highRoom - 1;
                    else
                        _finalY[i] = (value & 1) != 0 ? predicted - (value + 1) / 2 : predicted + value / 2;
                }
                else
                {
                    _step2[i] = false;
                    _finalY[i] = predicted;
                }
            }

            // Then the lines between the points that count, in the dB scale, looked up.
            int lx = 0, ly = _finalY[_sorted[0]] * _multiplier, hx = 0, hy = 0;
            for (int s = 1; s < _sorted.Length; s++)
            {
                int i = _sorted[s];
                if (!_step2[i]) continue;
                hy = _finalY[i] * _multiplier;
                hx = X[i];
                RenderLine(lx, ly, hx, hy, curve, n);
                lx = hx;
                ly = hy;
            }
            if (hx < n) RenderLine(hx, hy, n, hy, curve, n);
            return true;
        }

        private static int RenderPoint(int x0, int y0, int x1, int y1, int x)
        {
            int dy = y1 - y0, adx = x1 - x0;
            int offset = Math.Abs(dy) * (x - x0) / adx;
            return dy < 0 ? y0 - offset : y0 + offset;
        }

        private static void RenderLine(int x0, int y0, int x1, int y1, float[] curve, int n)
        {
            int dy = y1 - y0, adx = x1 - x0;
            int based = dy / adx;
            int sy = dy < 0 ? based - 1 : based + 1;
            int ady = Math.Abs(dy) - Math.Abs(based) * adx;
            int y = y0, err = 0;
            int end = Math.Min(x1, n);
            if (x0 < end) curve[x0] = InverseDb[Math.Clamp(y, 0, 255)];
            for (int x = x0 + 1; x < end; x++)
            {
                err += ady;
                if (err >= adx)
                {
                    err -= adx;
                    y += sy;
                }
                else
                {
                    y += based;
                }
                curve[x] = InverseDb[Math.Clamp(y, 0, 255)];
            }
        }

        // The specification's table: 256 steps of about 0.55 dB, geometric from 1.0649863e-07 up to 1.
        private static float[] BuildInverseDb()
        {
            var table = new float[256];
            for (int i = 0; i < 256; i++) table[i] = (float) Math.Pow(1.0649863e-07, (255 - i) / 255.0);
            return table;
        }
    }

    // The residue: per partition of the spectrum, a class (coded per channel, several partitions to a word)
    // picks up to eight books, one per pass, whose vectors add up to the values.
    private sealed class Residue
    {
        private readonly int _type, _begin, _end, _partitionSize, _classifications, _classbook;
        private readonly int[][] _books;    // by class, by pass: book or -1
        private int[][] _classes = [];                 // per channel decoded, per partition
        private readonly float[][] _interleaved = [[]];   // type 2's one vector

        public Residue(ref BitReader bits, Codebook[] books)
        {
            _type = (int) bits.Read(16);
            if (_type > 2) throw new InvalidDataException("A Vorbis residue type above 2");
            _begin = (int) bits.Read(24);
            _end = (int) bits.Read(24);
            _partitionSize = (int) bits.Read(24) + 1;
            _classifications = (int) bits.Read(6) + 1;
            _classbook = (int) bits.Read(8);
            if (_classbook >= books.Length) throw new InvalidDataException("A Vorbis residue's book is out of range");
            var cascade = new int[_classifications];
            for (int c = 0; c < _classifications; c++)
            {
                int low = (int) bits.Read(3);
                int high = bits.Read(1) == 1 ? (int) bits.Read(5) : 0;
                cascade[c] = high * 8 + low;
            }
            _books = new int[_classifications][];
            for (int c = 0; c < _classifications; c++)
            {
                _books[c] = new int[8];
                for (int pass = 0; pass < 8; pass++)
                {
                    _books[c][pass] = (cascade[c] & (1 << pass)) != 0 ? (int) bits.Read(8) : -1;
                    if (_books[c][pass] >= books.Length || (_books[c][pass] >= 0 && books[_books[c][pass]].Vectors == null))
                        throw new InvalidDataException("A Vorbis residue's book is out of range or has no vectors");
                }
            }
        }

        // The residue of the given channels into their vectors (n values each), zero where nothing is coded.
        public void Decode(ref BitReader bits, Codebook[] books, float[][] vectors, ReadOnlySpan<int> channels, bool[] used, int n)
        {
            foreach (int c in channels) Array.Clear(vectors[c], 0, n);
            Span<bool> decode = stackalloc bool[channels.Length];
            if (_type != 2)
            {
                for (int j = 0; j < channels.Length; j++) decode[j] = used[channels[j]];
                Decode(ref bits, books, vectors, channels, decode, n, _type);
                return;
            }
            // Type 2 codes the channels as one vector, interleaved, when any of them has a floor.
            bool any = false;
            foreach (int c in channels) any |= used[c];
            if (!any) return;
            int total = n * channels.Length;
            if (_interleaved[0].Length < total) _interleaved[0] = new float[total];
            float[] interleaved = _interleaved[0];
            Array.Clear(interleaved, 0, total);
            decode[0] = true;
            Decode(ref bits, books, _interleaved, [0], decode[..1], total, 1);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < channels.Length; j++) vectors[channels[j]][i] = interleaved[i * channels.Length + j];
        }

        // Into vectors[channels[j]] for each j that is to be decoded.
        private void Decode(ref BitReader bits, Codebook[] books, float[][] vectors, ReadOnlySpan<int> channels, ReadOnlySpan<bool> decode,
                            int size, int format)
        {
            int begin = Math.Min(_begin, size), end = Math.Min(_end, size);
            int partitions = (end - begin) / _partitionSize;
            if (partitions <= 0) return;
            Codebook classbook = books[_classbook];
            int perWord = classbook.Dimensions;
            if (_classes.Length < channels.Length) Array.Resize(ref _classes, channels.Length);
            for (int j = 0; j < channels.Length; j++)
                if (_classes[j] == null || _classes[j].Length < partitions + perWord) _classes[j] = new int[partitions + perWord];

            for (int pass = 0; pass < 8; pass++)
            {
                int partition = 0;
                while (partition < partitions)
                {
                    if (pass == 0)
                        for (int j = 0; j < channels.Length; j++)
                        {
                            if (!decode[j]) continue;
                            int word = classbook.Decode(ref bits);
                            if (word < 0) return;
                            for (int i = perWord - 1; i >= 0; i--)
                            {
                                _classes[j][partition + i] = word % _classifications;
                                word /= _classifications;
                            }
                        }
                    for (int i = 0; i < perWord && partition < partitions; i++, partition++)
                        for (int j = 0; j < channels.Length; j++)
                        {
                            if (!decode[j]) continue;
                            int book = _books[_classes[j][partition]][pass];
                            if (book < 0) continue;
                            if (!Partition(ref bits, books[book], vectors[channels[j]], begin + partition * _partitionSize, format)) return;
                        }
                }
            }
        }

        private bool Partition(ref BitReader bits, Codebook book, float[] vector, int offset, int format)
        {
            float[] vq = book.Vectors!;
            int dimensions = book.Dimensions;
            if (format == 0)
            {
                int step = _partitionSize / dimensions;
                for (int j = 0; j < step; j++)
                {
                    int entry = book.Decode(ref bits);
                    if (entry < 0) return false;
                    for (int k = 0; k < dimensions; k++) vector[offset + j + k * step] += vq[entry * dimensions + k];
                }
            }
            else
            {
                for (int i = 0; i < _partitionSize;)
                {
                    int entry = book.Decode(ref bits);
                    if (entry < 0) return false;
                    for (int k = 0; k < dimensions && i < _partitionSize; k++, i++) vector[offset + i] += vq[entry * dimensions + k];
                }
            }
            return true;
        }
    }

    // Which floor and residue each channel uses, and which channel pairs are coupled (as magnitude and angle).
    private sealed class Mapping
    {
        public readonly int[] Mux, SubmapFloor, SubmapResidue, Magnitude, Angle;

        public Mapping(ref BitReader bits, int channels, int floors, int residues)
        {
            if (bits.Read(16) != 0) throw new InvalidDataException("A Vorbis mapping type other than 0");
            int submaps = bits.Read(1) == 1 ? (int) bits.Read(4) + 1 : 1;
            int steps = bits.Read(1) == 1 ? (int) bits.Read(8) + 1 : 0;
            Magnitude = new int[steps];
            Angle = new int[steps];
            int channelBits = Ilog(channels - 1);
            for (int s = 0; s < steps; s++)
            {
                Magnitude[s] = (int) bits.Read(channelBits);
                Angle[s] = (int) bits.Read(channelBits);
                if (Magnitude[s] == Angle[s] || Magnitude[s] >= channels || Angle[s] >= channels)
                    throw new InvalidDataException("A Vorbis channel coupling out of range");
            }
            if (bits.Read(2) != 0) throw new InvalidDataException("A Vorbis mapping's reserved bits are set");
            Mux = new int[channels];
            if (submaps > 1)
                for (int c = 0; c < channels; c++)
                {
                    Mux[c] = (int) bits.Read(4);
                    if (Mux[c] >= submaps) throw new InvalidDataException("A Vorbis channel's submap is out of range");
                }
            SubmapFloor = new int[submaps];
            SubmapResidue = new int[submaps];
            for (int s = 0; s < submaps; s++)
            {
                bits.Read(8);
                SubmapFloor[s] = (int) bits.Read(8);
                SubmapResidue[s] = (int) bits.Read(8);
                if (SubmapFloor[s] >= floors || SubmapResidue[s] >= residues) throw new InvalidDataException("A Vorbis submap out of range");
            }
        }
    }

    private readonly record struct Mode(bool LongBlock, int Mapping);
}
