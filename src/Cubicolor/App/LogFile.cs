using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Cubicolor.Utilities;

namespace Cubicolor.App;

// The log a player can send along: log.txt beside their saves, the run before it kept as log.previous.txt. Every
// line is written through at once, so a crash loses none. A line repeating itself (a frame failing sixty times a
// second) is counted instead, and a file grown past its limit takes no more.
internal sealed class LogFile : IDisposable
{
    public const string Name = "log.txt", PreviousName = "log.previous.txt";

    private readonly StreamWriter _writer;
    private readonly long _limit;
    private readonly Func<DateTime> _now;
    private string? _last;
    private int _repeats;
    private bool _full;

    private LogFile(StreamWriter writer, long limit, Func<DateTime> now) => (_writer, _limit, _now) = (writer, limit, now);

    // Null when the file can't be opened: the game runs without a log then.
    public static LogFile? Open(string directory, long limit = 1 << 20, Func<DateTime>? now = null)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Name);
            if (File.Exists(path)) File.Move(path, Path.Combine(directory, PreviousName), overwrite: true);
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return new LogFile(new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true }, limit, now ?? (() => DateTime.Now));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"No log file in {directory}: {e.Message}");
            return null;
        }
    }

    // What the machine is, at the top of the log.
    public static string Machine(string version) =>
        $"{version}, {RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture}), running as {RuntimeInformation.ProcessArchitecture}, {Environment.ProcessorCount} cores";

    public void Write(LogLevel level, string message)
    {
        if (_full) return;
        try
        {
            string line = $"[{level}] {message}";
            if (line == _last)
            {
                _repeats++;
                return;
            }
            Repeats();
            _last = line;
            _writer.WriteLine($"{_now():yyyy-MM-dd HH:mm:ss.fff} {line}");
            if (_writer.BaseStream.Length < _limit) return;
            _full = true;
            _writer.WriteLine("The log is full.");
        }
        catch (IOException)
        {
            // A full disk must not take the game down with it.
            _full = true;
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_full) Repeats();
            _writer.Dispose();
        }
        catch (IOException)
        {
            _full = true;
        }
    }

    private void Repeats()
    {
        if (_repeats > 0) _writer.WriteLine($"    (the same {_repeats} more times)");
        _repeats = 0;
    }
}
