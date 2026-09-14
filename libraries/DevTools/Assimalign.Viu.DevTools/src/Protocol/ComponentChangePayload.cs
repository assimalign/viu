namespace Assimalign.Viu.DevTools;

/// <summary>The component change payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="ParentIdentifier">The direct component parent, or null for a root.</param>
/// <param name="Index">The zero-based sibling position.</param>
/// <param name="Name">The component, event, or member name.</param>
/// <param name="Key">The safely displayed reconciliation key.</param>
public sealed record ComponentChangePayload(
    int Identifier,
    int? ParentIdentifier,
    int Index,
    string Name,
    string? Key);
