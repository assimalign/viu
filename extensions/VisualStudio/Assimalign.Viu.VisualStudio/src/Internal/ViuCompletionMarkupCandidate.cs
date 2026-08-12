namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Decides, from a completion item's insertion text alone, whether the item names a markup element.
/// </summary>
/// <remarks>
/// <para>
/// Editor-free so the rule is unit-testable, in the same shape as
/// <see cref="ViuAutoClosingLogic"/>: the decision is here, and the Visual Studio wrapper that acts
/// on it holds no rule of its own.
/// </para>
/// <para>
/// The Language Server Protocol has no completion kind for a markup element, so an element arrives
/// carrying the nearest kind the protocol does have and is drawn with that kind's glyph. Recognizing
/// the element here is what lets the presentation be corrected. An element completion is the one
/// thing that inserts an open tag, and the casing after the bracket separates a native element from
/// a component exactly as name resolution does (<c>[CMP-6]</c>): a component's name is a C# type
/// name, so it is never lowercase.
/// </para>
/// </remarks>
internal static class ViuCompletionMarkupCandidate
{
    /// <summary>
    /// Returns whether the insertion text names a native or framework element rather than a
    /// component, an attribute, or an expression member.
    /// </summary>
    /// <param name="insertText">The item's insertion text, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for an element completion.</returns>
    public static bool IsElement(string? insertText) =>
        insertText is not null &&
        insertText.Length > 1 &&
        insertText[0] == '<' &&
        insertText[1] is >= 'a' and <= 'z';
}
