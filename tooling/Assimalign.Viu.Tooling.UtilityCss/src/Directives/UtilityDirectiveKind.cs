namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies a supported CSS-first utility directive.
/// </summary>
public enum UtilityDirectiveKind
{
    /// <summary>A static, complex, or functional custom utility definition.</summary>
    Utility,

    /// <summary>A project-defined selector or at-rule variant.</summary>
    CustomVariant,

    /// <summary>A variant applied to an authored CSS block.</summary>
    Variant,

    /// <summary>Utility candidates composed into an authored CSS rule.</summary>
    Apply,

    /// <summary>A stylesheet made available for composition without duplicated output.</summary>
    Reference,
}
