namespace Assimalign.Viu.Router;

/// <summary>
/// Options that tune how paths are matched. Anchoring is not configurable: the top-level matcher
/// always anchors the full path, so a pattern can never match a prefix of a longer path by accident.
/// </summary>
/// <remarks>
/// Immutable; use object initializers with <c>init</c> setters. The defaults are the forgiving ones
/// — non-strict (a trailing slash is tolerated) and case-insensitive — because either distinction
/// turns a working URL into a 404 for a user who typed it, so a route table opts into strictness
/// deliberately.
/// Specified by <c>[RTR-1]</c>.
/// </remarks>
public sealed class PathMatchingOptions
{
    /// <summary>The default options: non-strict and case-insensitive.</summary>
    public static PathMatchingOptions Default { get; } = new();

    /// <summary>
    /// When <see langword="true"/>, a trailing slash is significant (<c>/users</c> and
    /// <c>/users/</c> are distinct). When <see langword="false"/> (the default), a trailing slash is
    /// tolerated.
    /// </summary>
    public bool TrailingSlashSensitive { get; init; }

    /// <summary>
    /// When <see langword="true"/>, matching is case-sensitive. When <see langword="false"/> (the
    /// default), matching ignores case.
    /// </summary>
    public bool CaseSensitive { get; init; }
}
