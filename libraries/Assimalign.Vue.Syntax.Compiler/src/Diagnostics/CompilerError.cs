using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// A parse error with its code, human-readable message, and source location. The C# port of Vue 3.5's
/// <c>CompilerError</c> (<c>@vue/compiler-core</c> <c>errors.ts</c>). The parser reports errors through
/// <see cref="ParserOptions.OnError"/> rather than throwing, matching Vue's recoverable-parsing model.
/// A <see cref="SyntaxDiagnostic"/> whose language-specific code catalog and push delivery stay distinct
/// from the single-file-component parser's, per the shared base's per-language contract.
/// </summary>
public sealed record CompilerError : SyntaxDiagnostic
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
    }

    /// <summary>The error code.</summary>
    public CompilerErrorCode Code { get; init; }

    /// <inheritdoc />
    public override int RawCode => (int)Code;
}
