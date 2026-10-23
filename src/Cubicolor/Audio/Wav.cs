using System;
using System.IO;
using System.Text;

namespace Cubicolor.Audio;

// 16-bit PCM WAV files: read for the tools (the original recordings), written for captures.
public static class Wav
{
    public sealed record Clip(int SampleRate, int Channels, short[] Samples)
    {
        public int Frames => Samples.Length / Channels;
        public double Seconds => (double) Frames / SampleRate;
    }

    public static Clip Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException($"{path} is not a RIFF file");
        reader.ReadInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException($"{path} is not a WAVE file");
        int channels = 0, rate = 0, bits = 0;
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int size = reader.ReadInt32();
            long next = reader.BaseStream.Position + size + (size & 1);
            if (id == "fmt ")
            {
                int format = reader.ReadInt16();
                channels = reader.ReadInt16();
                rate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt16();
                bits = reader.ReadInt16();
                if (format != 1 || bits != 16) throw new InvalidDataException($"{path}: only 16-bit PCM is read");
            }
            else if (id == "data")
            {
                if (channels == 0) throw new InvalidDataException($"{path}: data before format");
                var samples = new short[size / 2];
                byte[] bytes = reader.ReadBytes(size);
                Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
                return new Clip(rate, channels, samples);
            }
            reader.BaseStream.Position = next;
        }
        throw new InvalidDataException($"{path} has no data");
    }

    public static void Write(string path, Clip clip)
    {
        using var writer = new BinaryWriter(File.Create(path));
        int bytes = clip.Samples.Length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + bytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short) 1);
        writer.Write((short) clip.Channels);
        writer.Write(clip.SampleRate);
        writer.Write(clip.SampleRate * clip.Channels * 2);
        writer.Write((short) (clip.Channels * 2));
        writer.Write((short) 16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(bytes);
        foreach (short s in clip.Samples) writer.Write(s);
    }

    public static short[] FromFloat(ReadOnlySpan<float> samples)
    {
        var result = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++) result[i] = (short) Math.Clamp(MathF.Round(samples[i] * 32767f), -32768, 32767);
        return result;
    }
}
