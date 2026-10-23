using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cubicolor.Utilities;

public sealed class AssertionException(string message) : Exception(message);

// Two flavors, per the architecture rules:
//   That  — always runs, Release included. Real invariants + untrusted/external data.
//   Debug — [Conditional("DEBUG")], stripped in Release (call AND argument elided).
//
// Both capture the source text of the condition via [CallerArgumentExpression] and append it to the
// thrown message, so a failure always names the exact check that failed and which flavor caught it.
// The layout is uniform whether or not a message was supplied — only the message part changes:
//   Check.That(library != IntPtr.Zero, $"Couldn't compile {name}: {ErrorText(error)}")
//     -> "Couldn't compile Blur.metal: <err> [assert: library != IntPtr.Zero]"
//   Check.Debug(_layouts.Count == 0)
//     -> "Assertion failed [debug assert: _layouts.Count == 0]"
//
// Note the asymmetry in what may be inlined into the condition: Check.That always runs, so calling
// straight into it (`Check.That(TryOpen(...), ...)`) is both safe and more legible than asserting on a
// local. Check.Debug is elided in Release, so its condition must stay side-effect free.
//
// Interpolated messages bind to the AssertMessage overloads, which defer evaluation until the
// assertion has actually failed — on the passing path the holes are never evaluated, so the
// error lookups scattered through the interop checks cost nothing.
public static class Check
{
    public static void That(bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] AssertMessage message,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw Failed(message.ToStringAndClear(), expression, debugOnly: false);
    }

    public static void That(bool condition, string message = "Assertion failed",
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw Failed(message, expression, debugOnly: false);
    }

    [Conditional("DEBUG")]
    public static void Debug(bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] AssertMessage message,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw Failed(message.ToStringAndClear(), expression, debugOnly: true);
    }

    [Conditional("DEBUG")]
    public static void Debug(bool condition, string message = "Assertion failed",
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw Failed(message, expression, debugOnly: true);
    }

    private static AssertionException Failed(string message, string? expression, bool debugOnly)
        => new($"{message} [{(debugOnly ? "debug assert" : "assert")}: {expression ?? "<unknown>"}]");
}

// Interpolated string handler for assertion messages. The `out bool shouldAppend` constructor
// parameter is the part that matters: the compiler emits the Append* calls only if it comes back
// true, so a passing assertion never evaluates the interpolation holes at all.
[InterpolatedStringHandler]
public ref struct AssertMessage
{
    private DefaultInterpolatedStringHandler _inner;

    public AssertMessage(int literalLength, int formattedCount, bool condition, out bool shouldAppend)
    {
        shouldAppend = !condition;
        _inner = shouldAppend ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string value) => _inner.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _inner.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _inner.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => _inner.AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string? format) => _inner.AppendFormatted(value, alignment, format);
    public void AppendFormatted(string? value) => _inner.AppendFormatted(value);

    internal string ToStringAndClear() => _inner.ToStringAndClear();
}
