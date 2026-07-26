namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The balanced utility token surrounding an editor cursor.
/// </summary>
/// <param name="Text">
/// The complete current token text. It may be incomplete candidate syntax while the author types.
/// </param>
/// <param name="Prefix">The token text from its start through the supplied cursor position.</param>
/// <param name="SourceSpan">The exact absolute span of <paramref name="Text"/>.</param>
public sealed record UtilityCandidateToken(
    string Text,
    string Prefix,
    UtilityCandidateSourceSpan SourceSpan);
