using System;
using System.Collections.Generic;
using System.IO;
using Cubicolor.Game;
using Cubicolor.Simulation.Saves;
using Cubicolor.Utilities;

namespace Cubicolor.Audio;

// The game's sound: the synthesized effects, made once at startup, and the music, played through the mixer
// into whatever the platform's output is (none headless; then a run can capture the mix instead).
public sealed class AudioService : IAudio
{
    private readonly Mixer _mixer = new();
    private readonly Dictionary<Sound, float[]> _sounds = [];
    private readonly IDisposable? _output;

    public Mixer Mixer => _mixer;

    public AudioService(string musicPath, string soundsDirectory, Func<Mixer, IDisposable?> output)
    {
        foreach (Sound sound in Enum.GetValues<Sound>()) _sounds[sound] = Synth.Make(sound);
        // The tile sound is the earlier versions' own recording (CC0, see assets/sounds/LICENSE.txt): the
        // synthesized one never came close to it.
        string tile = Path.Combine(soundsDirectory, "tile.wav");
        if (File.Exists(tile)) _sounds[Sound.Revive] = Stereo(Wav.Read(tile));
        else Log.Warn($"{tile} is missing; the tile sound is synthesized");
        if (File.Exists(musicPath))
        {
            try
            {
                _mixer.SetMusic(new Qoa.Decoder(File.ReadAllBytes(musicPath)));
            }
            catch (InvalidDataException e)
            {
                Log.Warn($"No music: {musicPath}: {e.Message}");
            }
        }
        else
        {
            Log.Warn($"No music: {musicPath} is missing");
        }
        _output = output(_mixer);
    }

    public void Play(Sound sound) => _mixer.Play(_sounds[sound]);

    // A clip as the mixer plays it: stereo floats at its rate.
    private static float[] Stereo(Wav.Clip clip)
    {
        if (clip.SampleRate != Mixer.Rate) throw new InvalidDataException($"A sound at {clip.SampleRate} Hz; the mixer runs at {Mixer.Rate}");
        var stereo = new float[clip.Frames * 2];
        for (int i = 0; i < clip.Frames; i++)
        {
            float left = clip.Samples[i * clip.Channels] / 32768f;
            stereo[2 * i] = left;
            stereo[2 * i + 1] = clip.Channels > 1 ? clip.Samples[i * clip.Channels + 1] / 32768f : left;
        }
        return stereo;
    }

    public void Apply(Settings settings) =>
        _mixer.SetVolumes(Mixer.Gain(settings.MusicVolume, settings.MusicMuted), Mixer.Gain(settings.SoundVolume, settings.SoundMuted));

    public void Dispose() => _output?.Dispose();
}
