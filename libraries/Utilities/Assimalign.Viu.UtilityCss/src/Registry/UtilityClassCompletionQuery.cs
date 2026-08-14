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
    /// Gets the maximum number of items returned. Zero returns no items while still reporting
    /// whether matching candidates exist.
    /// </summary>
    public int MaximumItems { get; init; } = DefaultMaximumItems;
}
