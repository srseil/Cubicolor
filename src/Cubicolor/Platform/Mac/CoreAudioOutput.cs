using System;
using System.Runtime.InteropServices;
using Cubicolor.Audio;
using Cubicolor.Game;
using Cubicolor.Utilities;

namespace Cubicolor.Platform.Mac;

// Sound out through Core Audio's AudioQueue: stereo 32-bit floats at the mixer's rate, three short buffers
// refilled from the mixer on the queue's own thread as each one finishes playing (about 32 ms of latency).
//
// A queue can stop asking for buffers for good: after the Mac has slept, or when the output device goes away.
// Watch, called every frame, notices it has gone quiet and builds a new one.
internal sealed unsafe partial class CoreAudioOutput : IAudioOutput
{
    private long _lastFill = Environment.TickCount64;
    private long _lastAttempt;
    private const int FramesPerBuffer = 512;
    private const int Buffers = 3;

    private IntPtr _queue;
    private GCHandle _self;
    private readonly Mixer _mixer;

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamDescription
    {
        public double SampleRate;
        public uint FormatId, FormatFlags, BytesPerPacket, FramesPerPacket, BytesPerFrame, ChannelsPerFrame, BitsPerChannel, Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueueBuffer
    {
        public uint Capacity;
        public void* Data;
        public uint ByteSize;
        public void* UserData;
        public uint PacketDescriptionCapacity;
        public void* PacketDescriptions;
        public uint PacketDescriptionCount;
    }

    private CoreAudioOutput(Mixer mixer) => _mixer = mixer;

    // An output playing `mixer`, or null (with a warning) when the system gives none: the game runs silent.
    public static CoreAudioOutput? Start(Mixer mixer)
    {
        var format = new StreamDescription
        {
            SampleRate = Mixer.Rate,
            FormatId = 0x6C70636D,     // 'lpcm'
            FormatFlags = 1 | 8,        // float, packed
            BytesPerPacket = 8,
            FramesPerPacket = 1,
            BytesPerFrame = 8,
            ChannelsPerFrame = 2,
            BitsPerChannel = 32,
        };
        var output = new CoreAudioOutput(mixer);
        output._self = GCHandle.Alloc(output);
        IntPtr queue;
        int status = AudioQueueNewOutput(&format, &Fill, GCHandle.ToIntPtr(output._self), IntPtr.Zero, IntPtr.Zero, 0, &queue);
        if (status != 0)
        {
            output._self.Free();
            Log.Warn($"No sound: AudioQueueNewOutput failed ({status})");
            return null;
        }
        output._queue = queue;
        output._lastFill = Environment.TickCount64;

        for (int i = 0; i < Buffers; i++)
        {
            QueueBuffer* buffer;
            if (AudioQueueAllocateBuffer(queue, FramesPerBuffer * 8, &buffer) != 0) continue;
            output.Render(buffer);
            AudioQueueEnqueueBuffer(queue, buffer, 0, IntPtr.Zero);
        }
        status = AudioQueueStart(queue, IntPtr.Zero);
        if (status != 0)
        {
            Log.Warn($"No sound: AudioQueueStart failed ({status})");
            output.Dispose();
            return null;
        }
        Log.Info($"Sound: Core Audio, {Mixer.Rate} Hz stereo");
        return output;
    }

    public void Watch()
    {
        long now = Environment.TickCount64;
        if (now - System.Threading.Interlocked.Read(ref _lastFill) < 500 || now - _lastAttempt < 1000) return;
        _lastAttempt = now;
        Log.Warn("Sound: the output went quiet (sleep or a device change); starting it again");
        Stop();
        if (Start(_mixer) is { } fresh)
        {
            // Take over the new queue; the new object only built it.
            _queue = fresh._queue;
            _self = fresh._self;
            _self.Target = this;
            fresh._queue = IntPtr.Zero;
            fresh._self = default;
            System.Threading.Interlocked.Exchange(ref _lastFill, Environment.TickCount64);
        }
    }

    private void Render(QueueBuffer* buffer)
    {
        System.Threading.Interlocked.Exchange(ref _lastFill, Environment.TickCount64);
        var samples = new Span<float>(buffer->Data, FramesPerBuffer * 2);
        _mixer.Render(samples);
        buffer->ByteSize = FramesPerBuffer * 8;
    }

    // The queue has played a buffer: fill it again and put it back. Runs on the queue's own thread.
    [UnmanagedCallersOnly]
    private static void Fill(IntPtr user, IntPtr queue, QueueBuffer* buffer)
    {
        if (GCHandle.FromIntPtr(user).Target is not CoreAudioOutput output || output._queue == IntPtr.Zero) return;
        try
        {
            output.Render(buffer);
        }
        catch (Exception e)
        {
            // An exception must not cross into Core Audio's thread; play silence instead.
            new Span<byte>(buffer->Data, (int) buffer->Capacity).Clear();
            Log.Error($"Sound failed: {e.Message}");
        }
        AudioQueueEnqueueBuffer(queue, buffer, 0, IntPtr.Zero);
    }

    private void Stop()
    {
        IntPtr queue = _queue;
        _queue = IntPtr.Zero;
        if (queue != IntPtr.Zero)
        {
            AudioQueueStop(queue, 1);
            AudioQueueDispose(queue, 1);
        }
        if (_self.IsAllocated) _self.Free();
    }

    public void Dispose() => Stop();

    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueNewOutput(StreamDescription* format, delegate* unmanaged<IntPtr, IntPtr, QueueBuffer*, void> callback,
                                                   IntPtr userData, IntPtr runLoop, IntPtr runLoopMode, uint flags, IntPtr* queue);
    [LibraryImport(AudioToolbox)] private static partial int AudioQueueAllocateBuffer(IntPtr queue, uint bytes, QueueBuffer** buffer);
    [LibraryImport(AudioToolbox)] private static partial int AudioQueueEnqueueBuffer(IntPtr queue, QueueBuffer* buffer, uint packets, IntPtr descriptions);
    [LibraryImport(AudioToolbox)] private static partial int AudioQueueStart(IntPtr queue, IntPtr startTime);
    [LibraryImport(AudioToolbox)] private static partial int AudioQueueStop(IntPtr queue, byte immediate);
    [LibraryImport(AudioToolbox)] private static partial int AudioQueueDispose(IntPtr queue, byte immediate);
}
