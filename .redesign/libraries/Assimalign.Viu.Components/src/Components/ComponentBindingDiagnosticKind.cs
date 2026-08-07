namespace Assimalign.Viu.Components;

/// <summary>
/// Categories of invocation-resolution diagnostics.
/// </summary>
public enum ComponentBindingDiagnosticKind
{
    /// <summary>A parameter declared required was not supplied and has no default.</summary>
    MissingRequiredParameter,

    /// <summary>A supplied value was rejected by the declared parameter validator.</summary>
    ParameterValidationFailed,

    /// <summary>Two supplied names alias the same declared parameter.</summary>
    DuplicateAlias,
}
