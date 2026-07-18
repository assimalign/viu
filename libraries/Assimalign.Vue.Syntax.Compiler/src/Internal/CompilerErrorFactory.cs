namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// Builds <see cref="CompilerError"/> diagnostics from a code and location, resolving the human-readable
/// message from <see cref="CompilerErrorMessages"/>. The transform-stage analogue of the parser's error
/// emission and of Vue 3.5's <c>createCompilerError</c> (<c>@vue/compiler-core</c> <c>errors.ts</c>).
/// </summary>
internal static class CompilerErrorFactory
{
    /// <summary>Creates a diagnostic for <paramref name="code"/> at <paramref name="location"/>.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="location">The source location the diagnostic points at.</param>
    public static CompilerError Create(CompilerErrorCode code, SourceLocation location)
        => new(code, CompilerErrorMessages.GetMessage(code), location);
}
