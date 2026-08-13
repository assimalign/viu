namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One immutable name/value token in the built-in utility theme.
/// </summary>
/// <param name="Name">The class-facing token name.</param>
/// <param name="Value">The emitted CSS value.</param>
public sealed record UtilityThemeToken(
    string Name,
    string Value);
