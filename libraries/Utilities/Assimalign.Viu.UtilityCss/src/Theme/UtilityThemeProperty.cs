namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One immutable semantic custom property in a utility theme.
/// </summary>
/// <param name="Name">The canonical unprefixed custom-property name, beginning with <c>--</c>.</param>
/// <param name="Value">The authored CSS value.</param>
/// <param name="Options">The declaration behavior that supplied the current value.</param>
public sealed record UtilityThemeProperty(
    string Name,
    string Value,
    UtilityThemeOptions Options);
