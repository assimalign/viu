namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Builds <see cref="CompilerError"/> diagnostics from a code and location, resolving the human-readable
/// message from <see cref="CompilerErrorMessages"/>. The transform stage emits diagnostics this way
/// rather than through <c>ParserOptions.OnError</c>, because a transform reports against a node it
/// already holds.
/// </summary>
internal static class CompilerErrorFactory
{
    /// <summary>Creates a diagnostic for <paramref name="code"/> at <paramref name="location"/>.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="location">The source location the diagnostic points at.</param>
    public static CompilerError Create(CompilerErrorCode code, SourceLocation location)
        => new(code, CompilerErrorMessages.GetMessage(code), location);
}
