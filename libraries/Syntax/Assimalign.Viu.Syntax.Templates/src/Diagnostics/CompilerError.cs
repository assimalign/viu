using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A parse error with its code, human-readable message, and source location. The parser
/// <em>pushes</em> errors through <see cref="ParserOptions.OnError"/> as it goes rather than throwing,
/// so one pass reports every problem in a malformed template and still returns a tree.
/// A <see cref="Diagnostic"/> whose language-specific code catalog and push delivery stay distinct
/// from the single-file-component parser's, per the shared base's per-language contract.
/// </summary>
public sealed record CompilerError : Diagnostic
{
    /// <summary>Creates a compiler error for <paramref name="code"/> at <paramref name="location"/>.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The human-readable message for <paramref name="code"/>.</param>
    /// <param name="location">The source location the error points at (a zero-width range at the offending offset).</param>
    [SetsRequiredMembers]
    public CompilerError(CompilerErrorCode code, string message, SourceLocation location)
    {
        Code = code;
        Message = message;
        Location = location;
        // Every compiler error is error severity: this catalog carries no warning-severity codes, so
        // severity is a property of the type, not of the instance.
        Severity = DiagnosticSeverity.Error;
    }

    /// <summary>The error code.</summary>
    public CompilerErrorCode Code { get; init; }

    /// <inheritdoc />
    public override int RawCode => (int)Code;
}
