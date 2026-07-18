using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// A recoverable parse diagnostic: its code, human-readable message, and source location. Modeled on
/// <c>Assimalign.Vue.Syntax.Templates</c>'s <c>CompilerError</c> but carrying the SingleFileComponent
/// area's own <see cref="SingleFileComponentErrorCode"/> catalog. The parser reports these through
/// <see cref="SingleFileComponentParseResult.Errors"/> and never throws for malformed input, matching
/// Vue's recoverable-parsing model (<c>@vue/compiler-sfc</c> <c>parse().errors</c>). A
/// <see cref="Diagnostic"/> whose Vuecs-defined code catalog and result-errors delivery stay
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
        // The whole Vuecs-defined catalog is recoverable *errors*, mirroring @vue/compiler-sfc's
        // parse().errors; a warning tier would be a new catalog decision, not a per-instance choice.
        Severity = DiagnosticSeverity.Error;
    }

    /// <summary>The diagnostic code.</summary>
    public SingleFileComponentErrorCode Code { get; init; }

    /// <inheritdoc />
    public override int RawCode => (int)Code;
}
