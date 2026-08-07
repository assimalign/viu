namespace Assimalign.Viu.Components;

/// <summary>
/// Categories of invocation-resolution diagnostics.
/// </summary>
/// <remarks>Produced by the pure binding transformation specified by <c>[CMP-2]</c>.</remarks>
public enum ComponentBindingDiagnosticKind
{
    /// <summary>A parameter declared required was not supplied and has no default.</summary>
    MissingRequiredParameter,

    /// <summary>A supplied value was rejected by the declared parameter validator.</summary>
    ParameterValidationFailed,

    /// <summary>Two declarations share an alias or two supplied aliases target one parameter.</summary>
    DuplicateAlias,
}
