using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A recoverable CSS parse diagnostic: its <see cref="Code"/>, human-readable message, and source
/// location. Modeled on the single-file-component parser's <c>SingleFileComponentError</c> but carrying
/// the CSS area's own <see cref="CssErrorCode"/> catalog. The parser reports these on its result and
/// never throws for malformed input, matching CSS Syntax Module Level 3's error-recovery model
/// (https://www.w3.org/TR/css-syntax-3/#error-handling). A <see cref="Diagnostic"/> whose Viu-defined
/// code catalog stays distinct from the other parsers', per the shared base's per-language contract.
/// </summary>
public sealed record CssError : Diagnostic
{
    /// <summary>Creates a diagnostic for <paramref name="code"/> at <paramref name="location"/>.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="location">The source range the diagnostic points at.</param>
    [SetsRequiredMembers]
    public CssError(CssErrorCode code, SourceLocation location)
    {
        Code = code;
        Message = CssErrorMessages.GetMessage(code);
        Location = location;
        // The whole Viu-defined catalog is recoverable errors, mirroring the single-file-component
        // parser; a warning tier would be a new catalog decision, not a per-instance choice.
        Severity = DiagnosticSeverity.Error;
    }

    /// <summary>The diagnostic code.</summary>
    public CssErrorCode Code { get; init; }

    /// <inheritdoc />
    public override int RawCode => (int)Code;
}
