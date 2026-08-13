namespace Assimalign.Viu.Router;

/// <summary>
/// The specificity weights used to rank compiled path patterns: static segments outrank dynamic,
/// dynamic outrank the catch-all wildcard, and a custom parameter pattern edges out the bare
/// parameter pattern. It is the <em>relative</em> magnitudes that are the contract — changing any
/// single weight silently reorders route tables that were resolving correctly, so treat the scale
/// as frozen and pin any change with a ranking test.
/// </summary>
/// <remarks>
/// Held as <see cref="double"/> because the strict and case-sensitive bonuses are fractional
/// (0.7 and 0.25) and must stay below 1 so they can only break a tie, never overturn a whole-weight
/// difference. Every weight is written as a multiple of <see cref="Multiplier"/> so the ordering is
/// legible at a glance instead of buried in magic numbers.
/// </remarks>
internal static class PathScore
{
    /// <summary>The base multiplier every other weight is expressed against.</summary>
    public const double Multiplier = 10;

    /// <summary>Score of the empty root segment "/".</summary>
    public const double Root = 9 * Multiplier;

    /// <summary>Base score every populated segment starts from.</summary>
    public const double Segment = 4 * Multiplier;

    /// <summary>Score contributed by a sub-segment of tokens.</summary>
    public const double SubSegment = 3 * Multiplier;

    /// <summary>Bonus for a static token.</summary>
    public const double Static = 4 * Multiplier;

    /// <summary>Bonus for a dynamic parameter token.</summary>
    public const double Dynamic = 2 * Multiplier;

    /// <summary>Extra bonus when a parameter supplies a custom pattern.</summary>
    public const double BonusCustomPattern = 1 * Multiplier;

    /// <summary>
    /// Penalty for a catch-all wildcard (<c>(.*)</c>); it also cancels the custom-pattern bonus so
    /// a wildcard always ranks below any other dynamic segment.
    /// </summary>
    public const double BonusWildcard = (-4 * Multiplier) - BonusCustomPattern;

    /// <summary>Penalty for a repeatable parameter (<c>+</c>/<c>*</c>).</summary>
    public const double BonusRepeatable = -2 * Multiplier;

    /// <summary>Penalty for an optional parameter (<c>?</c>/<c>*</c>).</summary>
    public const double BonusOptional = -0.8 * Multiplier;

    /// <summary>
    /// Fractional bonus applied once to the last score when strict matching is on; kept under 1 so
    /// it only breaks ties.
    /// </summary>
    public const double BonusTrailingSlashSensitive = 0.07 * Multiplier;

    /// <summary>
    /// Fractional bonus per segment when case-sensitive matching is on; kept under 1 so it only
    /// breaks ties.
    /// </summary>
    public const double BonusCaseSensitive = 0.025 * Multiplier;
}
