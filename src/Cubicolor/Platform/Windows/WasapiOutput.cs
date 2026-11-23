using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cubicolor.Audio;
using Cubicolor.Game;
using Cubicolor.Utilities;

namespace Cubicolor.Platform.Windows;

// Sound out through WASAPI, shared mode: stereo 32-bit floats at the mixer's rate (Windows converts to the
// device's own format), on a thread of its own woken by the device whenever it wants more (about 30 ms queued).
//
// The default device can change (headphones plugged in, a choice in Windows' settings) and a device can go
// away. Watch, called every frame, notices either and builds a new output on the device that is now default.
internal sealed unsafe partial class WasapiOutput : IAudioOutput
{
    private static readonly Guid MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid IAudioRenderClient = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
    private static readonly Guid FloatFormat = new("00000003-0000-0010-8000-00AA00389B71");   // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT

    private const long BufferDuration = 300_000;   // 30 ms, in 100 ns units
    private const int DeviceInvalidated = unchecked((int) 0x88890004);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WaveFormat
    {
        public ushort Tag, Channels;
        public uint SamplesPerSecond, BytesPerSecond;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
        public ushort ValidBits;
        public uint ChannelMask;
        public Guid SubFormat;
    }

    private readonly Mixer _mixer;
    private IntPtr _enumerator, _client, _render, _event;
    private string _deviceId = "";
    private uint _bufferFrames;
    private Thread? _thread;
    private volatile bool _stopping, _failed;
    private long _lastDeviceCheck = Environment.TickCount64, _lastAttempt;

    private WasapiOutput(Mixer mixer) => _mixer = mixer;

    // An output playing `mixer`, or null (with a warning) when the system gives none: the game runs silent.
    public static WasapiOutput? Start(Mixer mixer)
    {
        var output = new WasapiOutput(mixer);
        try
        {
            output.Open();
            Log.Info($"Sound: WASAPI, {Mixer.Rate} Hz stereo");
            return output;
        }
        catch (InvalidOperationException e)
        {
            Log.Warn($"No sound: {e.Message}");
            output.Dispose();
            return null;
        }
    }

    private void Open()
    {
        CoInitializeEx(IntPtr.Zero, 0 /* COINIT_MULTITHREADED */);
        Guid clsid = MMDeviceEnumerator, iid = IMMDeviceEnumerator;
        IntPtr enumerator;
        Com.Check(CoCreateInstance(&clsid, IntPtr.Zero, 0x17 /* CLSCTX_ALL */, &iid, &enumerator), "Creating the device enumerator");
        _enumerator = enumerator;

        IntPtr device = DefaultDevice(_enumerator) ?? throw new InvalidOperationException("no output device");
        try
        {
            _deviceId = DeviceId(device);
            // IMMDevice::Activate (3)
            Guid clientIid = IAudioClient;
            IntPtr client;
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint, IntPtr, IntPtr*, int>) Com.Slot(device, 3))(device, &clientIid, 0x17, IntPtr.Zero, &client),
                "Activating the audio client");
            _client = client;
        }
        finally
        {
            Com.Release(ref device);
        }

        var format = new WaveFormat
        {
            Tag = 0xFFFE,   // WAVE_FORMAT_EXTENSIBLE
            Channels = 2,
            SamplesPerSecond = Mixer.Rate,
            BytesPerSecond = Mixer.Rate * 8,
            BlockAlign = 8,
            BitsPerSample = 32,
            ExtraSize = 22,
            ValidBits = 32,
            ChannelMask = 0x3,   // front left, front right
            SubFormat = FloatFormat,
        };
        // IAudioClient::Initialize (3): shared, woken by an event, converting from our format to the device's.
        const uint flags = 0x00040000 /* EVENTCALLBACK */ | 0x80000000 /* AUTOCONVERTPCM */ | 0x08000000 /* SRC_DEFAULT_QUALITY */;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, int, uint, long, long, WaveFormat*, Guid*, int>) Com.Slot(_client, 3))(
            _client, 0, flags, BufferDuration, 0, &format, null), "Initializing the audio client");
        uint frames;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>) Com.Slot(_client, 4))(_client, &frames), "GetBufferSize");
        _bufferFrames = frames;
        _event = CreateEventW(IntPtr.Zero, 0, 0, null);
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>) Com.Slot(_client, 13))(_client, _event), "SetEventHandle");
        // IAudioClient::GetService (14)
        Guid renderIid = IAudioRenderClient;
        IntPtr render;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>) Com.Slot(_client, 14))(_client, &renderIid, &render), "GetService");
        _render = render;

        // A full buffer before starting, so playback doesn't begin with an underrun.
        Fill();
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, int>) Com.Slot(_client, 10))(_client), "Starting the audio client");
        _stopping = _failed = false;
        _thread = new Thread(Run) { Name = "Sound", IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
    }

    private static IntPtr? DefaultDevice(IntPtr enumerator)
    {
        // IMMDeviceEnumerator::GetDefaultAudioEndpoint (4): render, console role.
        IntPtr device;
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>) Com.Slot(enumerator, 4))(enumerator, 0, 0, &device);
        return hr < 0 ? null : device;
    }

    private static string DeviceId(IntPtr device)
    {
        // IMMDevice::GetId (5)
        char* id;
        if (((delegate* unmanaged[Stdcall]<IntPtr, char**, int>) Com.Slot(device, 5))(device, &id) < 0) return "";
        string result = new(id);
        CoTaskMemFree(id);
        return result;
    }

    private void Run()
    {
        AvSetMmThreadCharacteristicsW("Games", out _);
        while (!_stopping)
        {
            WaitForSingleObject(_event, 200);
            if (_stopping) break;
            try
            {
                if (!Fill()) break;
            }
            catch (Exception e)
            {
                // An exception must not end the process from this thread; the output restarts instead.
                Log.Error($"Sound failed: {e.Message}");
                _failed = true;
                break;
            }
        }
    }

    // Tops the device's buffer up from the mixer; false when the device has gone.
    private bool Fill()
    {
        uint padding;
        // IAudioClient::GetCurrentPadding (6)
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>) Com.Slot(_client, 6))(_client, &padding);
        if (hr == DeviceInvalidated) return Fail();
        Com.Check(hr, "GetCurrentPadding");
        uint frames = _bufferFrames - padding;
        if (frames == 0) return true;
        // IAudioRenderClient::GetBuffer (3), ReleaseBuffer (4)
        byte* data;
        hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint, byte**, int>) Com.Slot(_render, 3))(_render, frames, &data);
        if (hr == DeviceInvalidated) return Fail();
        Com.Check(hr, "GetBuffer");
        _mixer.Render(new Span<float>(data, (int) frames * 2));
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>) Com.Slot(_render, 4))(_render, frames, 0), "ReleaseBuffer");
        return true;
    }

    private bool Fail()
    {
        _failed = true;
        return false;
    }

    public void Watch()
    {
        long now = Environment.TickCount64;
        bool moved = false;
        if (!_failed && now - _lastDeviceCheck >= 2000)
        {
            _lastDeviceCheck = now;
            if (_enumerator != IntPtr.Zero && DefaultDevice(_enumerator) is { } device)
            {
                moved = DeviceId(device) != _deviceId;
                Com.Release(ref device);
            }
        }
        if (!_failed && !moved) return;
        if (now - _lastAttempt < 1000) return;
        _lastAttempt = now;
        Log.Warn(moved ? "Sound: the default output device changed; moving to it" : "Sound: the output device went away; starting again");
        Stop();
        try
        {
            Open();
        }
        catch (InvalidOperationException e)
        {
            Log.Warn($"No sound: {e.Message}");
            Stop();
            _failed = true;
        }
    }

    private void Stop()
    {
        _stopping = true;
        if (_event != IntPtr.Zero) SetEvent(_event);
        _thread?.Join();
        _thread = null;
        if (_client != IntPtr.Zero) ((delegate* unmanaged[Stdcall]<IntPtr, int>) Com.Slot(_client, 11))(_client);   // Stop
        Com.Release(ref _render);
        Com.Release(ref _client);
        Com.Release(ref _enumerator);
        if (_event != IntPtr.Zero) CloseHandle(_event);
        _event = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    [LibraryImport("ole32.dll")] private static partial int CoInitializeEx(IntPtr reserved, uint flags);
    [LibraryImport("ole32.dll")] private static partial int CoCreateInstance(Guid* clsid, IntPtr outer, uint context, Guid* iid, IntPtr* instance);
    [LibraryImport("ole32.dll")] private static partial void CoTaskMemFree(void* memory);
    [LibraryImport("kernel32.dll")] private static partial IntPtr CreateEventW(IntPtr attributes, int manualReset, int initialState, char* name);
    [LibraryImport("kernel32.dll")] private static partial int SetEvent(IntPtr handle);
    [LibraryImport("kernel32.dll")] private static partial uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [LibraryImport("kernel32.dll")] private static partial int CloseHandle(IntPtr handle);
    [LibraryImport("avrt.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial IntPtr AvSetMmThreadCharacteristicsW(string task, out uint index);
}
