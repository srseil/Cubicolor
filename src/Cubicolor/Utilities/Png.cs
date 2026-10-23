using System;
using System.IO;
using System.IO.Compression;

namespace Cubicolor.Utilities;

// A minimal PNG encoder: 8-bit RGBA truecolour, one IDAT chunk, filter type 0 (None) on every scanline,
// zlib compression via the runtime's ZLibStream. Just enough to save screenshots without pulling in
// SDL_image — deliberately not a general-purpose PNG library (no palettes, no interlacing, no other
// colour types). See the spec: https://www.w3.org/TR/png/.
public static class Png
{
    // PNG file signature (fixed 8 bytes).
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    // CRC-32 (IEEE) lookup table, built once; PNG checksums each chunk's type + data with it.
    private static readonly uint[] CrcTable = BuildCrcTable();

    // Encode `pixels` (a tightly packed width*height*4 byte buffer, row 0 = top) to `path` as an RGBA PNG.
    // `bgra` swaps R and B per pixel, for sources whose bytes are B,G,R,A (SDL ARGB8888) rather than the
    // R,G,B,A (SDL ABGR8888) that PNG wants. `opaque` writes every alpha as 255, whatever the source holds.
    public static void Write(string path, ReadOnlySpan<byte> pixels, int width, int height, bool bgra, bool opaque = false)
    {
        int stride = width * 4;

        // Raw image data as PNG expects it before compression: each scanline is a filter-type byte (0 =
        // None) followed by its pixels, already reordered to R,G,B,A.
        var raw = new byte[height * (1 + stride)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0;   // filter: None
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int p = row + x * 4;
                byte c0 = pixels[p], c1 = pixels[p + 1], c2 = pixels[p + 2], c3 = opaque ? (byte) 255 : pixels[p + 3];
                if (bgra) { raw[o++] = c2; raw[o++] = c1; raw[o++] = c0; raw[o++] = c3; }
                else      { raw[o++] = c0; raw[o++] = c1; raw[o++] = c2; raw[o++] = c3; }
            }
        }

        // Deflate the whole thing into a single zlib stream (ZLibStream writes the header + Adler-32).
        byte[] idat;
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw, 0, raw.Length);
            idat = compressed.ToArray();
        }

        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        file.Write(Signature, 0, Signature.Length);

        Span<byte> ihdr = stackalloc byte[13];
        WriteBigEndian(ihdr, (uint) width);
        WriteBigEndian(ihdr[4..], (uint) height);
        ihdr[8] = 8;    // bit depth per channel
        ihdr[9] = 6;    // colour type: truecolour with alpha (RGBA)
        ihdr[10] = 0;   // compression method: deflate
        ihdr[11] = 0;   // filter method: adaptive (only type 0 used here)
        ihdr[12] = 0;   // interlace method: none
        WriteChunk(file, "IHDR", ihdr);
        WriteChunk(file, "IDAT", idat);
        WriteChunk(file, "IEND", ReadOnlySpan<byte>.Empty);
    }

    // Write one PNG chunk: length, 4-char type, data, then a CRC-32 over type + data (all big-endian).
    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        WriteBigEndian(header, (uint) data.Length);
        for (int i = 0; i < 4; i++) header[4 + i] = (byte) type[i];
        stream.Write(header);
        stream.Write(data);

        uint crc = Crc32(header.Slice(4, 4), data);
        Span<byte> crcBytes = stackalloc byte[4];
        WriteBigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte) (value >> 24);
        destination[1] = (byte) (value >> 16);
        destination[2] = (byte) (value >> 8);
        destination[3] = (byte) value;
    }

    private static uint Crc32(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte x in a) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        foreach (byte x in b) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
