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

    public AudioService(string musicPath, Func<Mixer, IDisposable?> output)
    {
        foreach (Sound sound in Enum.GetValues<Sound>()) _sounds[sound] = Synth.Make(sound);
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

    public void Apply(Settings settings) =>
        _mixer.SetVolumes(Mixer.Gain(settings.MusicVolume, settings.MusicMuted), Mixer.Gain(settings.SoundVolume, settings.SoundMuted));

    public void Dispose() => _output?.Dispose();
}
