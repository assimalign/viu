namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Immutable public metadata for one utility family in the executable registry.
/// </summary>
/// <param name="Root">The candidate root recognized by the parser and resolver.</param>
/// <param name="Description">The editor-facing family description.</param>
/// <param name="Order">The stable property-order bucket used by generated CSS.</param>
/// <param name="SupportsNegativeValues">
/// Whether the family accepts the candidate grammar's leading negative form.
/// </param>
/// <param name="CompletionCandidates">
/// Representative complete candidates offered by editor integrations.
/// </param>
public sealed record UtilityDefinition(
    string Root,
    string Description,
    int Order,
    bool SupportsNegativeValues,
    UtilityCollection<string> CompletionCandidates);
