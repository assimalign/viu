namespace Assimalign.Viu.Router;

/// <summary>
/// The specificity weights used to rank compiled path patterns. The C# port of vue-router's
/// <c>PathScore</c> const enum (<c>packages/router/src/matcher/pathParserRanker.ts</c>),
/// reproduced value-for-value so ranking matches upstream exactly: static segments outrank
/// dynamic, dynamic outrank the catch-all wildcard, and custom patterns edge out the bare
/// parameter pattern.
/// </summary>
/// <remarks>
/// Held as <see cref="double"/> because the strict and case-sensitive bonuses are fractional
/// (0.7 and 0.25) and must stay below 1 so they never overturn a whole-weight difference. The
/// weights are declared exactly as upstream multiples of <see cref="Multiplier"/> so the
/// derivation stays legible against the reference.
/// </remarks>
internal static class PathScore
{
    /// <summary>The base multiplier every other weight is expressed against. Upstream <c>_multiplier = 10</c>.</summary>
    public const double Multiplier = 10;

    /// <summary>Score of the empty root segment "/". Upstream <c>Root = 9 * _multiplier</c>.</summary>
    public const double Root = 9 * Multiplier;

    /// <summary>Base score every populated segment starts from. Upstream <c>Segment = 4 * _multiplier</c>.</summary>
    public const double Segment = 4 * Multiplier;

    /// <summary>Score contributed by a sub-segment of tokens. Upstream <c>SubSegment = 3 * _multiplier</c>.</summary>
    public const double SubSegment = 3 * Multiplier;

    /// <summary>Bonus for a static token. Upstream <c>Static = 4 * _multiplier</c>.</summary>
    public const double Static = 4 * Multiplier;

    /// <summary>Bonus for a dynamic parameter token. Upstream <c>Dynamic = 2 * _multiplier</c>.</summary>
    public const double Dynamic = 2 * Multiplier;

    /// <summary>Extra bonus when a parameter supplies a custom pattern. Upstream <c>BonusCustomRegExp = 1 * _multiplier</c>.</summary>
    public const double BonusCustomPattern = 1 * Multiplier;

    /// <summary>
    /// Penalty for a catch-all wildcard (<c>(.*)</c>); it also cancels the custom-pattern bonus so
    /// a wildcard always ranks below any other dynamic segment. Upstream
    /// <c>BonusWildcard = -4 * _multiplier - BonusCustomRegExp</c>.
    /// </summary>
    public const double BonusWildcard = (-4 * Multiplier) - BonusCustomPattern;

    /// <summary>Penalty for a repeatable parameter (<c>+</c>/<c>*</c>). Upstream <c>BonusRepeatable = -2 * _multiplier</c>.</summary>
    public const double BonusRepeatable = -2 * Multiplier;

    /// <summary>Penalty for an optional parameter (<c>?</c>/<c>*</c>). Upstream <c>BonusOptional = -0.8 * _multiplier</c>.</summary>
    public const double BonusOptional = -0.8 * Multiplier;

    /// <summary>
    /// Fractional bonus applied once to the last score when strict matching is on; kept under 1 so
    /// it only breaks ties. Upstream <c>BonusStrict = 0.07 * _multiplier</c>.
    /// </summary>
    public const double BonusStrict = 0.07 * Multiplier;

    /// <summary>
    /// Fractional bonus per segment when case-sensitive matching is on; kept under 1 so it only
    /// breaks ties. Upstream <c>BonusCaseSensitive = 0.025 * _multiplier</c>.
    /// </summary>
    public const double BonusCaseSensitive = 0.025 * Multiplier;
}
