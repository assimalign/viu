using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Owns the serialized comment-marker vocabulary shared by server rendering, host-tree parsing,
/// and Core hydration.
/// </summary>
/// <remarks>
/// These values are a cross-package wire contract. They are centralized here so a host cannot
/// silently emit a marker that Core's hydration walker does not recognize. Specified by
/// <c>[SSR-MARKERS-1]</c> through <c>[SSR-MARKERS-3]</c>.
/// </remarks>
public static class HydrationMarkers
{
    /// <summary>Gets the serialized comment that opens a fragment range.</summary>
    public const string FragmentStart = "<!--[-->";

    /// <summary>Gets the serialized comment that closes a fragment range.</summary>
    public const string FragmentEnd = "<!--]-->";

    /// <summary>Gets the serialized empty-comment placeholder.</summary>
    public const string EmptyComment = "<!---->";

    /// <summary>Gets the serialized comment that opens a teleport's logical range.</summary>
    public const string TeleportStart = "<!--teleport start-->";

    /// <summary>Gets the serialized comment that closes a teleport's logical range.</summary>
    public const string TeleportEnd = "<!--teleport end-->";

    /// <summary>Gets the serialized comment that terminates one teleport target range.</summary>
    public const string TeleportAnchor = "<!--teleport anchor-->";

    /// <summary>Gets the serialized comment that closes a deferred hydration range.</summary>
    public const string LazyHydrationEnd = "<!--lazy hydration end-->";

    /// <summary>Gets the opening marker for a non-immediate hydration strategy.</summary>
    /// <param name="kind">The deferred strategy kind.</param>
    /// <returns>The stable serialized opening comment.</returns>
    public static string GetLazyHydrationStart(HydrationStrategyKind kind) => kind switch
    {
        HydrationStrategyKind.Idle => "<!--lazy hydration idle-->",
        HydrationStrategyKind.Visible => "<!--lazy hydration visible-->",
        HydrationStrategyKind.MediaQuery => "<!--lazy hydration media query-->",
        HydrationStrategyKind.Interaction => "<!--lazy hydration interaction-->",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            "An immediate or unknown strategy has no deferred hydration marker."),
    };

    internal const string FragmentStartData = "[";
    internal const string FragmentEndData = "]";
    internal const string EmptyCommentData = "";
    internal const string TeleportStartData = "teleport start";
    internal const string TeleportEndData = "teleport end";
    internal const string TeleportAnchorData = "teleport anchor";
    internal const string LazyHydrationEndData = "lazy hydration end";

    internal static string GetLazyHydrationStartData(HydrationStrategyKind kind) =>
        GetLazyHydrationStart(kind)[4..^3];

    internal static bool IsLazyHydrationStartData(string data) =>
        string.Equals(data, "lazy hydration idle", StringComparison.Ordinal)
        || string.Equals(data, "lazy hydration visible", StringComparison.Ordinal)
        || string.Equals(data, "lazy hydration media query", StringComparison.Ordinal)
        || string.Equals(data, "lazy hydration interaction", StringComparison.Ordinal);
}
