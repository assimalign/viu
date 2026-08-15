namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Immutable input for one bounded utility-class completion lookup.
/// </summary>
public sealed record UtilityClassCompletionQuery
{
    /// <summary>
    /// The default maximum number of completion items returned by one lookup. The budget bounds
    /// serialization and editor work even when a theme expands to more than one hundred thousand
    /// candidates.
    /// </summary>
    public const int DefaultMaximumItems = 500;

    /// <summary>
    /// Gets the default unfiltered query with the engine completion budget.
    /// </summary>
    public static UtilityClassCompletionQuery Default { get; } = new();

    /// <summary>
    /// Gets the optional complete-candidate prefix. Variant chains and a configured theme prefix are
    /// composed by the engine rather than requiring a consumer to rewrite candidate text.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets whether completion results include variant-prefixed composites. The default preserves
    /// live editor completion behavior; catalog producers can disable variants to enumerate the
    /// finite base-class surface without multiplying it by breakpoint variants. The build-catalog
    /// boundary is tracked by <c>[V01.01.12.30]</c>.
    /// </summary>
    public bool IncludeVariants { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of items returned. Zero returns no items while still reporting
    /// whether matching candidates exist.
    /// </summary>
    public int MaximumItems { get; init; } = DefaultMaximumItems;
}
