using System;
using System.Globalization;

namespace Assimalign.Viu.Router;

/// <summary>
/// Decodes the flat, primitives-only array a single batched read returns from the JS edge into a
/// <see cref="BrowserHistorySnapshot"/>. Isolating the wire format here (rather than inside the
/// browser-only interop) keeps the state round-trip — the [V01.01.08.02] position/scroll criterion —
/// unit-testable in a plain .NET host with a hand-built array standing in for the JS payload.
/// </summary>
/// <remarks>
/// The payload is a single <c>string[]</c> (one interop crossing, no per-property getters): raw
/// <c>location</c> components, a history length, then the current entry's state. A <see langword="null"/>
/// link (<c>back</c>/<c>forward</c>) and an absent state or scroll are encoded as an empty string plus
/// a boolean flag, so the array never carries a null element to marshal.
/// </remarks>
internal static class BrowserHistorySnapshotMarshaller
{
    /// <summary>The number of slots in the flat snapshot array.</summary>
    internal const int FieldCount = 14;

    private const int PathnameIndex = 0;
    private const int SearchIndex = 1;
    private const int HashIndex = 2;
    private const int HostIndex = 3;
    private const int HistoryLengthIndex = 4;
    private const int HasStateIndex = 5;
    private const int BackIndex = 6;
    private const int CurrentIndex = 7;
    private const int ForwardIndex = 8;
    private const int ReplacedIndex = 9;
    private const int PositionIndex = 10;
    private const int HasScrollIndex = 11;
    private const int ScrollLeftIndex = 12;
    private const int ScrollTopIndex = 13;

    /// <summary>
    /// Decodes a batched-read payload. An empty <c>back</c>/<c>forward</c> slot decodes to
    /// <see langword="null"/>; a false <c>hasState</c> flag yields a snapshot with no state.
    /// </summary>
    /// <param name="raw">The flat array from <c>history.readSnapshot()</c> (length <see cref="FieldCount"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="raw"/> has the wrong length.</exception>
    internal static BrowserHistorySnapshot Decode(string[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Length != FieldCount)
        {
            throw new ArgumentException(
                $"History snapshot payload must have {FieldCount} fields but had {raw.Length}.",
                nameof(raw));
        }

        var state = DecodeFlag(raw[HasStateIndex])
            ? new RouterHistoryState(
                Back: EmptyToNull(raw[BackIndex]),
                Current: raw[CurrentIndex],
                Forward: EmptyToNull(raw[ForwardIndex]),
                Replaced: DecodeFlag(raw[ReplacedIndex]),
                Position: DecodeInteger(raw[PositionIndex]),
                Scroll: DecodeFlag(raw[HasScrollIndex])
                    ? new ScrollPosition(DecodeDouble(raw[ScrollLeftIndex]), DecodeDouble(raw[ScrollTopIndex]))
                    : null)
            : null;

        return new BrowserHistorySnapshot(
            Pathname: raw[PathnameIndex],
            Search: raw[SearchIndex],
            Hash: raw[HashIndex],
            Host: raw[HostIndex],
            HistoryLength: DecodeInteger(raw[HistoryLengthIndex]),
            State: state);
    }

    private static string? EmptyToNull(string value)
        => value.Length == 0 ? null : value;

    private static bool DecodeFlag(string value)
        => value == "1";

    private static int DecodeInteger(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double DecodeDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0d;
}
