namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One successfully parsed custom-property declaration or reset in source order.
/// </summary>
/// <param name="Name">The canonical unprefixed custom-property or wildcard-reset name.</param>
/// <param name="Value">The authored CSS value.</param>
/// <param name="Options">The enclosing <c>@theme</c> options.</param>
/// <param name="IsReset">
/// Whether <paramref name="Value"/> is <c>initial</c> and removes a property or namespace.
/// </param>
/// <param name="SourceSpan">The complete declaration source span.</param>
public sealed record UtilityThemeDeclaration(
    string Name,
    string Value,
    UtilityThemeOptions Options,
    bool IsReset,
    UtilityThemeSourceSpan SourceSpan);
