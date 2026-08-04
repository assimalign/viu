namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One structurally valid CSS-first source directive.
/// </summary>
/// <param name="Kind">Whether the directive includes or excludes a path or inline expression.</param>
/// <param name="Value">The decoded path or inline brace-expansion expression.</param>
/// <param name="Candidates">
/// The deterministic expanded candidate set for inline directives, or an empty collection for path
/// directives.
/// </param>
/// <param name="SourceSpan">The exact complete directive span.</param>
public sealed record UtilitySourceDirective(
    UtilitySourceDirectiveKind Kind,
    string Value,
    UtilityCollection<string> Candidates,
    UtilityStylesheetSourceSpan SourceSpan);
