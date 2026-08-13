namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One source-located argument to a supported build-time CSS function.
/// </summary>
/// <param name="Text">The authored, trimmed argument text.</param>
/// <param name="Kind">The structural argument kind.</param>
/// <param name="SourceSpan">The exact argument source span.</param>
public sealed record UtilityCssFunctionArgument(
    string Text,
    UtilityCssFunctionArgumentKind Kind,
    UtilityStylesheetSourceSpan SourceSpan);
