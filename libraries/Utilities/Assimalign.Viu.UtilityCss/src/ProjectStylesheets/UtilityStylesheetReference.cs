namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One host-resolved edge in a project stylesheet reference graph.
/// </summary>
/// <param name="ReferencingSourceIdentity">
/// The source identity containing the matching <c>@reference</c> directive.
/// </param>
/// <param name="Specifier">The decoded specifier authored in the directive.</param>
/// <param name="ResolvedSourceIdentity">The stable identity of the referenced stylesheet.</param>
/// <param name="Css">The complete referenced stylesheet content.</param>
public sealed record UtilityStylesheetReference(
    string ReferencingSourceIdentity,
    string Specifier,
    string ResolvedSourceIdentity,
    string Css);
