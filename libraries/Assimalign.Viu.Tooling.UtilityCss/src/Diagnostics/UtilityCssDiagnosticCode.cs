namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies a recoverable utility resolution or compilation diagnostic.
/// </summary>
public enum UtilityCssDiagnosticCode
{
    /// <summary>The candidate grammar is malformed.</summary>
    InvalidCandidate,

    /// <summary>The candidate uses accepted compatibility syntax that should be updated.</summary>
    DeprecatedSyntax,

    /// <summary>The utility family is not registered in the active compatibility catalog.</summary>
    UnsupportedUtility,

    /// <summary>The utility family does not accept the supplied value.</summary>
    UnsupportedValue,

    /// <summary>The utility family does not accept the supplied slash modifier.</summary>
    UnsupportedModifier,

    /// <summary>The utility family does not accept a negative form.</summary>
    UnsupportedNegativeForm,

    /// <summary>The candidate uses a variant not registered in the active compatibility catalog.</summary>
    UnsupportedVariant,

    /// <summary>An arbitrary property or value could terminate its generated declaration.</summary>
    UnsafeArbitraryValue,
}
