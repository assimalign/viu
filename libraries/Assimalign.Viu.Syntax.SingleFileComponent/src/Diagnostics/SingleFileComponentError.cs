using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A recoverable parse diagnostic: its code, human-readable message, catalog severity, and source
/// location. Modeled on <c>Assimalign.Viu.Syntax.Templates</c>'s <c>CompilerError</c> but carrying the
/// SingleFileComponent area's own <see cref="SingleFileComponentErrorCode"/> catalog. The parser reports
/// these through <see cref="SingleFileComponentParseResult.Errors"/> or
/// <see cref="VueSingleFileComponentParseResult.Errors"/> and never throws for malformed input,
/// matching Vue's recoverable-parsing model (<c>@vue/compiler-sfc</c> <c>parse().errors</c>). A
/// <see cref="Diagnostic"/> whose Viu-defined code catalog and result-errors delivery stay
/// distinct from the template compiler's, per the shared base's per-language contract.
/// </summary>
public sealed record SingleFileComponentError : Diagnostic
{
    /// <summary>Creates a diagnostic for <paramref name="code"/> at <paramref name="location"/>.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="message">The human-readable message for <paramref name="code"/>.</param>
    /// <param name="location">The source range the diagnostic points at.</param>
    [SetsRequiredMembers]
    public SingleFileComponentError(SingleFileComponentErrorCode code, string message, SourceLocation location)
    {
        Code = code;
        Message = message;
        Location = location;
        // Severity is a catalog decision, made per code, never per instance: the [V01.01.06.10]
        // legacy-container codes are warnings (the block still parses during the migration window);
        // every other code stays a recoverable error, mirroring @vue/compiler-sfc's parse().errors.
        Severity = SingleFileComponentErrorMessages.GetSeverity(code);
    }

    /// <summary>The diagnostic code.</summary>
    public SingleFileComponentErrorCode Code { get; init; }

    /// <inheritdoc />
    public override int RawCode => (int)Code;
}
