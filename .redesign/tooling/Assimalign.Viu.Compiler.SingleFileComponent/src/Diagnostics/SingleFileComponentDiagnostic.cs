using System;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>Describes one stable editor-neutral compiler diagnostic.</summary>
/// <remarks>Specified by <c>[SFC-DIAG-1]</c> and <c>[TOOL-5]</c>.</remarks>
public sealed class SingleFileComponentDiagnostic
{
    /// <summary>Initializes a diagnostic value.</summary>
    public SingleFileComponentDiagnostic(
        string identifier,
        string message,
        SingleFileComponentDiagnosticSeverity severity,
        SingleFileComponentSourceRange range,
        string? helpLink = null)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("A diagnostic identifier is required.", nameof(identifier));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("A diagnostic message is required.", nameof(message));
        }

        Identifier = identifier;
        Message = message;
        Severity = severity;
        Range = range ?? throw new ArgumentNullException(nameof(range));
        HelpLink = helpLink;
    }

    /// <summary>Gets the stable diagnostic identifier.</summary>
    public string Identifier { get; }

    /// <summary>Gets the developer-facing message.</summary>
    public string Message { get; }

    /// <summary>Gets the editor-neutral severity.</summary>
    public SingleFileComponentDiagnosticSeverity Severity { get; }

    /// <summary>Gets the source range.</summary>
    public SingleFileComponentSourceRange Range { get; }

    /// <summary>Gets the optional documentation link.</summary>
    public string? HelpLink { get; }
}
