using System;
using System.Buffers.Binary;
using System.IO;

namespace Cubicolor.Audio;

// The Ogg container (RFC 3533): a stream of pages, each a 27-byte header ("OggS", version, flags, granule
// position, serial, sequence, CRC, segment count), a table of segment lengths and the segments. A packet is a
// run of segments ending in one shorter than 255 bytes, and may carry on into the next page. One logical
// stream is read; pages that fail their checksum end it.
internal sealed class OggReader
{
    public const int HeaderLength = 27;

    private static readonly uint[] CrcTable = BuildCrcTable();

    private readonly Stream _stream;
    private readonly byte[] _page = new byte[HeaderLength + 255 + 255 * 255];
    private int _segments, _segment, _bodyAt;     // the current page's segment count, the next one, where it starts
    private byte[] _packet = new byte[1 << 16];

    public OggReader(Stream stream) => _stream = stream;

    // Where the next page starts, for Seek; only meaningful between whole pages.
    public long PagePosition => _stream.Position;

    public void Seek(long pagePosition)
    {
        _stream.Position = pagePosition;
        _segments = _segment = 0;
    }

    // The next packet, valid until the next call; false at the end of the stream (or where it breaks off).
    public bool NextPacket(out byte[] packet, out int length)
    {
        packet = _packet;
        length = 0;
        while (true)
        {
            if (_segment == _segments)
            {
                if (!ReadPage()) return false;
                continue;
            }
            int size = _page[HeaderLength + _segment];
            if (length + size > _packet.Length) Array.Resize(ref _packet, Math.Max(_packet.Length * 2, length + size));
            packet = _packet;
            Buffer.BlockCopy(_page, _bodyAt, _packet, length, size);
            length += size;
            _bodyAt += size;
            _segment++;
            if (size < 255) return true;
        }
    }

    // True when the current page has no packet left in it: the next packet starts on a fresh page.
    public bool AtPageEnd => _segment == _segments;

    private bool ReadPage()
    {
        if (_stream.ReadAtLeast(_page.AsSpan(0, HeaderLength), HeaderLength, throwOnEndOfStream: false) < HeaderLength) return false;
        if (!_page.AsSpan(0, 4).SequenceEqual("OggS"u8) || _page[4] != 0) return false;
        int segments = _page[26];
        if (_stream.ReadAtLeast(_page.AsSpan(HeaderLength, segments), segments, throwOnEndOfStream: false) < segments) return false;
        int body = 0;
        for (int i = 0; i < segments; i++) body += _page[HeaderLength + i];
        int bodyAt = HeaderLength + segments;
        if (_stream.ReadAtLeast(_page.AsSpan(bodyAt, body), body, throwOnEndOfStream: false) < body) return false;
        if (!ChecksumMatches(_page.AsSpan(0, bodyAt + body))) return false;
        _segments = segments;
        _segment = 0;
        _bodyAt = bodyAt;
        return true;
    }

    // The granule position of the stream's last page, which for Vorbis is the number of sample frames in it:
    // pages are looked for backwards from the end, by their capture pattern and checksum.
    public static long LastGranule(Stream stream)
    {
        long end = stream.Length;
        var window = new byte[Math.Min(end, HeaderLength + 255 + 255 * 255 + HeaderLength)];
        stream.Position = end - window.Length;
        stream.ReadExactly(window);
        for (int at = window.Length - HeaderLength; at >= 0; at--)
        {
            if (window[at] != 'O' || !window.AsSpan(at, 4).SequenceEqual("OggS"u8)) continue;
            int segments = window[at + 26];
            if (at + HeaderLength + segments > window.Length) continue;
            int length = HeaderLength + segments;
            for (int i = 0; i < segments; i++) length += window[at + HeaderLength + i];
            if (at + length > window.Length || !ChecksumMatches(window.AsSpan(at, length))) continue;
            long granule = BinaryPrimitives.ReadInt64LittleEndian(window.AsSpan(at + 6));
            if (granule >= 0) return granule;
        }
        throw new InvalidDataException("No Ogg page with a granule position at the end");
    }

    // The CRC-32 Ogg uses (polynomial 0x04c11db7, not reflected, initial value 0), with the CRC field as zeros.
    private static bool ChecksumMatches(ReadOnlySpan<byte> page)
    {
        uint crc = 0;
        for (int i = 0; i < page.Length; i++)
        {
            byte b = i is >= 22 and < 26 ? (byte) 0 : page[i];
            crc = (crc << 8) ^ CrcTable[(crc >> 24) ^ b];
        }
        return crc == BinaryPrimitives.ReadUInt32LittleEndian(page[22..]);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i << 24;
            for (int j = 0; j < 8; j++) r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04c11db7 : r << 1;
            table[i] = r;
        }
        return table;
    }
}
