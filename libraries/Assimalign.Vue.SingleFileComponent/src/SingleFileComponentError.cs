namespace Assimalign.Vue.SingleFileComponent;

/// <summary>
/// A recoverable parse diagnostic: its code, human-readable message, and source location. Modeled on
/// <c>Assimalign.Vue.Compiler</c>'s <c>CompilerError</c> but carrying the SingleFileComponent area's own
/// <see cref="SingleFileComponentErrorCode"/> catalog. The parser reports these through <see cref="SingleFileComponentParseResult.Errors"/>
/// and never throws for malformed input, matching Vue's recoverable-parsing model
/// (<c>@vue/compiler-sfc</c> <c>parse().errors</c>).
/// </summary>
/// <param name="Code">The diagnostic code.</param>
/// <param name="Message">The human-readable message for <paramref name="Code"/>.</param>
/// <param name="Location">The source range the diagnostic points at.</param>
public sealed record SingleFileComponentError(SingleFileComponentErrorCode Code, string Message, SourceLocation Location);
