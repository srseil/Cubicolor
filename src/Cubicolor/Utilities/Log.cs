using System;

namespace Cubicolor.Utilities;

// How much a log line matters, in rising order. The names are what a log line shows ("[Warn]").
public enum LogLevel { Debug, Info, Warn, Error }

// The one way the game logs. Every line goes to the sink the composition root installs (App's Logging: the
// log file, stderr, the console panel, a command's captured output); until there is one, or below its
// level, to stderr or nowhere. Thread-safe: the sink is called under a lock, so lines from the recorder's
// writer thread or the control server never interleave.
public static class Log
{
    private static readonly object Gate = new();
    private static Action<LogLevel, string>? _sink;
    private static LogLevel _minimum = LogLevel.Info;

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    // Where lines go from now on, and the least level that is written; null restores stderr.
    public static void SetSink(Action<LogLevel, string>? sink, LogLevel minimum)
    {
        lock (Gate)
        {
            _sink = sink;
            _minimum = minimum;
        }
    }

    private static void Write(LogLevel level, string message)
    {
        lock (Gate)
        {
            if (level < _minimum) return;
            if (_sink is { } sink) sink(level, message);
            else Console.Error.WriteLine($"[{level}] {message}");
        }
    }
}
