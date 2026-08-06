namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies a recoverable project-stylesheet semantic diagnostic.
/// </summary>
public enum UtilityProjectStylesheetDiagnosticCode
{
    /// <summary>A host-supplied reference edge was not found.</summary>
    UnresolvedReference,

    /// <summary>A reference path returned to a stylesheet already being visited.</summary>
    CyclicReference,

    /// <summary>An <c>@apply</c> candidate is not a built-in or project-defined utility.</summary>
    UnknownAppliedUtility,

    /// <summary>Custom utilities recursively apply each other.</summary>
    CircularApply,

    /// <summary>Custom variants recursively compose each other.</summary>
    CircularVariant,

    /// <summary>A functional custom utility could not resolve a required value or modifier.</summary>
    UnresolvedFunctionalValue,

    /// <summary>A parsed variant could not be transformed into executable CSS.</summary>
    UnsupportedVariant,

    /// <summary>A function resolved to an unsafe CSS value and was omitted.</summary>
    UnsafeResolvedValue,
}
