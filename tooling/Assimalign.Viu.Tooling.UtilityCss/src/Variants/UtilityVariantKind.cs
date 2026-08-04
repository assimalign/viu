namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies the grammar shape of a utility variant.
/// </summary>
public enum UtilityVariantKind
{
    /// <summary>A configured prefix occupying the first variant position.</summary>
    Prefix,

    /// <summary>A static variant with no value or modifier.</summary>
    Static,

    /// <summary>A functional variant with an optional named or arbitrary value.</summary>
    Functional,

    /// <summary>A compound variant wrapping another variant.</summary>
    Compound,

    /// <summary>An arbitrary selector or at-rule variant.</summary>
    Arbitrary,
}
