namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One executable static or functional <c>@utility</c> definition.
/// </summary>
/// <param name="Name">The authored utility name or final <c>-*</c> pattern.</param>
/// <param name="Body">The balanced authored declaration block.</param>
/// <param name="SourceSpan">The complete definition source span.</param>
/// <param name="BodySourceSpan">The exact authored declaration-block content span.</param>
/// <param name="IsReferenced">
/// Whether the definition came through <c>@reference</c> and therefore contributes no direct output.
/// </param>
public sealed record UtilityCustomUtilityDefinition(
    string Name,
    string Body,
    UtilityStylesheetSourceSpan SourceSpan,
    UtilityStylesheetSourceSpan BodySourceSpan,
    bool IsReferenced)
{
    /// <summary>
    /// Gets whether this definition accepts a candidate value.
    /// </summary>
    public bool IsFunctional => Name.EndsWith("-*", System.StringComparison.Ordinal);
}
