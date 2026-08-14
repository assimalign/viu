namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// The deterministic result of one bounded utility-class completion lookup.
/// </summary>
/// <param name="Items">Matching metadata in compiler order, bounded by the query budget.</param>
/// <param name="IsTruncated">
/// Whether at least one additional matching item exists after <paramref name="Items"/>.
/// </param>
public sealed record UtilityClassCompletionResult(
    UtilityCollection<UtilityClassMetadata> Items,
    bool IsTruncated);
