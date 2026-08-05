namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The two indentation strings a brace block takes when <c>Enter</c> is pressed between an
/// auto-completed <c>{</c> and <c>}</c> ([V01.01.12.07.09]).
/// </summary>
/// <param name="ClosingBraceIndentation">
/// The whitespace the closing brace's own line begins with. It is the opening brace line's leading
/// whitespace copied verbatim, so the closer lines up under the construct that opened it.
/// </param>
/// <param name="CaretIndentation">
/// The whitespace the caret's line begins with: <paramref name="ClosingBraceIndentation"/> plus one
/// indent level, so the caret lands one level inside the block.
/// </param>
/// <remarks>
/// A value type with no editor dependency, so the computation that produces it stays unit-testable
/// while only the buffer edit that applies it is runtime-verified.
/// </remarks>
internal readonly record struct ViuBlockExpansion(
    string ClosingBraceIndentation,
    string CaretIndentation);
