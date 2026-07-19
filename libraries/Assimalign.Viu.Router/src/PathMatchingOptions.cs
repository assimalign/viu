namespace Assimalign.Viu.Router;

/// <summary>
/// Options that tune how paths are matched. The C# port of the matcher-relevant subset of
/// vue-router's <c>PathParserOptions</c> (<c>packages/router/src/matcher/pathParserRanker.ts</c>).
/// The <c>start</c>/<c>end</c> options are not exposed because the top-level matcher always anchors
/// the full path.
/// </summary>
/// <remarks>
/// Immutable; use object initializers with <c>init</c> setters. The defaults match vue-router:
/// non-strict (a trailing slash is tolerated) and case-insensitive.
/// </remarks>
public sealed class PathMatchingOptions
{
    /// <summary>The default options: non-strict, case-insensitive (matching vue-router's defaults).</summary>
    public static PathMatchingOptions Default { get; } = new();

    /// <summary>
    /// When <see langword="true"/>, a trailing slash is significant (<c>/users</c> and
    /// <c>/users/</c> are distinct). When <see langword="false"/> (the default), a trailing slash is
    /// tolerated. Upstream <c>strict</c>.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// When <see langword="true"/>, matching is case-sensitive. When <see langword="false"/> (the
    /// default), matching ignores case. Upstream <c>sensitive</c>.
    /// </summary>
    public bool Sensitive { get; init; }
}
