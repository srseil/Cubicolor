using System;
using System.IO;
using Cubicolor.App;
using Cubicolor.Utilities;
using Xunit;

namespace Cubicolor.Tests;

public sealed class LogFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cubicolor-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LogFile Open(long limit = 1 << 20) => LogFile.Open(_root, limit, () => new DateTime(2026, 11, 22, 12, 0, 0))!;

    private string Read(string name) => File.ReadAllText(Path.Combine(_root, name)).ReplaceLineEndings("\n");

    [Fact]
    public void Lines_are_on_disk_while_the_game_still_runs()
    {
        using LogFile log = Open();

        log.Write(LogLevel.Warn, "No sound");

        using var reader = new StreamReader(new FileStream(Path.Combine(_root, LogFile.Name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        Assert.Equal("2026-11-22 12:00:00.000 [Warn] No sound\n", reader.ReadToEnd().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_run_before_is_kept()
    {
        using (LogFile first = Open()) first.Write(LogLevel.Info, "first");
        using (LogFile second = Open()) second.Write(LogLevel.Info, "second");

        Assert.Contains("first", Read(LogFile.PreviousName));
        Assert.Contains("second", Read(LogFile.Name));
        Assert.DoesNotContain("first", Read(LogFile.Name));
    }

    [Fact]
    public void A_line_that_repeats_is_counted()
    {
        using (LogFile log = Open())
        {
            for (int i = 0; i < 1000; i++) log.Write(LogLevel.Error, "Frame failed");
            log.Write(LogLevel.Info, "done");
        }

        string[] lines = Read(LogFile.Name).TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("the same 999 more times", lines[1]);
    }

    [Fact]
    public void A_log_stops_at_its_limit()
    {
        using (LogFile log = Open(limit: 2000))
            for (int i = 0; i < 1000; i++) log.Write(LogLevel.Info, $"line {i}");

        Assert.InRange(new FileInfo(Path.Combine(_root, LogFile.Name)).Length, 2000, 2100);
        Assert.EndsWith("The log is full.\n", Read(LogFile.Name));
    }

    [Fact]
    public void Without_a_place_to_write_there_is_no_log_and_no_crash()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "taken");
        File.WriteAllText(file, "");

        Assert.Null(LogFile.Open(Path.Combine(file, "data")));
    }
}
