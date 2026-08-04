namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One structurally parsed build-time CSS function call.
/// </summary>
/// <param name="Kind">The supported function kind.</param>
/// <param name="Text">The complete authored function call.</param>
/// <param name="Arguments">Balanced, source-located arguments in authored order.</param>
/// <param name="SourceSpan">The complete function-call source span.</param>
/// <param name="IsValid">Whether the call has no structural, argument, or placement error.</param>
public sealed record UtilityCssFunction(
    UtilityCssFunctionKind Kind,
    string Text,
    UtilityCollection<UtilityCssFunctionArgument> Arguments,
    UtilityStylesheetSourceSpan SourceSpan,
    bool IsValid);
