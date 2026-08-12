namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Decides, from a line of text and a caret position alone, how committing a Viu attribute
/// completion should edit the buffer.
/// </summary>
/// <remarks>
/// <para>
/// Editor-free so the rule is unit-testable, in the same shape as <see cref="ViuAutoClosingLogic"/>:
/// the decision is here, and the Visual Studio adapter that applies it holds no rule of its own.
/// </para>
/// <para>
/// An attribute completion writes an empty value — <c>title=""</c> — and the value is where the
/// author is going, so the caret belongs between the quotes. A completion item can only ask for that
/// by carrying a snippet tabstop, which this editor does not expand; the caret would otherwise land
/// past the closing quote and every attribute would need a keystroke to get back inside it.
/// </para>
/// <para>
/// Computing the replaced span here rather than accepting the one inferred from the typed word is
/// the other half. A shorthand (<c>:</c>, <c>@</c>) and the punctuation around the caret are not
/// word characters, so an inferred span both leaves the shorthand in place — committing beside it
/// as <c>::title</c> — and can reach across a neighbouring <c>&gt;</c> and consume it.
/// </para>
/// </remarks>
internal static class ViuCompletionCommitLogic
{
    /// <summary>
    /// Returns how to commit an attribute completion, or <see langword="null"/> when the item is not
    /// one and the editor's own commit applies unchanged.
    /// </summary>
    /// <param name="line">The text of the line the caret is on, without its line break.</param>
    /// <param name="caretIndex">The zero-based caret offset within that line.</param>
    /// <param name="insertText">The committed item's insertion text.</param>
    /// <returns>The edit to apply, or <see langword="null"/> to decline.</returns>
    public static ViuCompletionCommitEdit? GetAttributeCommitEdit(
        string? line,
        int caretIndex,
        string? insertText)
    {
        // Only the empty-value form is claimed. Anything else - an element, an expression member, a
        // valueless directive - commits the way it always did.
        if (line is null ||
            insertText is null ||
            caretIndex < 0 ||
            caretIndex > line.Length ||
            insertText.Length < 3 ||
            !insertText.EndsWith("=\"\"", System.StringComparison.Ordinal))
        {
            return null;
        }

        int start = caretIndex;
        while (start > 0 && IsAttributeNameCharacter(line[start - 1]))
        {
            start--;
        }

        return new ViuCompletionCommitEdit(
            start,
            caretIndex - start,
            insertText,
            insertText.Length - 1);
    }

    // The characters an attribute name is spelled from, its shorthand and its argument separator
    // included: ':' and '@' open one, and 'v-on:click.prevent' carries the rest.
    private static bool IsAttributeNameCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '@' or '#';
}
