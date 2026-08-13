namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One structurally parsed CSS-first utility directive.
/// </summary>
/// <param name="Kind">The supported directive kind.</param>
/// <param name="Parameters">The authored directive parameters without the at-rule name.</param>
/// <param name="Identifier">
/// The utility or custom-variant name, or the decoded reference specifier when applicable.
/// </param>
/// <param name="Body">The authored block content, or <see langword="null"/> for a statement.</param>
/// <param name="NestingDepth">The CSS block depth at which the directive begins.</param>
/// <param name="SourceSpan">The complete directive source span.</param>
/// <param name="ParametersSourceSpan">The exact trimmed parameter span.</param>
/// <param name="BodySourceSpan">The exact block-content span when the directive has a block.</param>
/// <param name="IsValid">Whether the node has no structural or placement error.</param>
public sealed record UtilityDirective(
    UtilityDirectiveKind Kind,
    string Parameters,
    string? Identifier,
    string? Body,
    int NestingDepth,
    UtilityStylesheetSourceSpan SourceSpan,
    UtilityStylesheetSourceSpan ParametersSourceSpan,
    UtilityStylesheetSourceSpan? BodySourceSpan,
    bool IsValid)
{
    /// <summary>
    /// Gets whether this directive uses a declaration block rather than statement syntax.
    /// </summary>
    public bool HasBlock => Body is not null;
}
