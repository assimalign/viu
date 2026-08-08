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

    internal const string FragmentStartData = "[";
    internal const string FragmentEndData = "]";
    internal const string EmptyCommentData = "";
    internal const string TeleportStartData = "teleport start";
    internal const string TeleportEndData = "teleport end";
    internal const string TeleportAnchorData = "teleport anchor";
}
