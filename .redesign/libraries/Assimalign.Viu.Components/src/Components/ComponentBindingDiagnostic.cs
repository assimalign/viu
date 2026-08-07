using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// One diagnostic produced while resolving an invocation against a contract. The runtime decides
/// routing and per-mount gating (for example, warning about a missing required parameter only on
/// the initial mount).
/// </summary>
/// <remarks>Specified by <c>[CMP-12]</c>, <c>[CMP-13]</c>, and <c>[CMP-17]</c>.</remarks>
public sealed class ComponentBindingDiagnostic
{
    /// <summary>Initializes a resolution diagnostic.</summary>
    /// <param name="kind">The diagnostic category.</param>
    /// <param name="name">The parameter, event, or binding name involved.</param>
    /// <param name="message">The human-readable description.</param>
    public ComponentBindingDiagnostic(ComponentBindingDiagnosticKind kind, string name, string message)
    {
        if (kind is not ComponentBindingDiagnosticKind.MissingRequiredParameter
            and not ComponentBindingDiagnosticKind.ParameterValidationFailed
            and not ComponentBindingDiagnosticKind.DuplicateAlias)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Kind = kind;
        Name = name;
        Message = message;
    }

    /// <summary>Gets the diagnostic category.</summary>
    public ComponentBindingDiagnosticKind Kind { get; }

    /// <summary>Gets the parameter, event, or binding name involved.</summary>
    public string Name { get; }

    /// <summary>Gets the human-readable description.</summary>
    public string Message { get; }
}
