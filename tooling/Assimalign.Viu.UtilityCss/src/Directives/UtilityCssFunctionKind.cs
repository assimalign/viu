namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies a supported build-time CSS function.
/// </summary>
public enum UtilityCssFunctionKind
{
    /// <summary>Resolves the value portion of a functional custom utility.</summary>
    Value,

    /// <summary>Resolves the slash modifier of a functional custom utility.</summary>
    Modifier,

    /// <summary>Supplies the value used when a functional utility part is omitted.</summary>
    Default,

    /// <summary>Calculates a value against the configured spacing scale.</summary>
    Spacing,

    /// <summary>Applies an opacity value to a color.</summary>
    Alpha,
}
