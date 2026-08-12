using System.Collections.Generic;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Decides, from document text and a caret position alone, whether a <c>Tab</c> means "expand the
/// shortcut I just typed".
/// </summary>
/// <remarks>
/// <para>
/// Editor-free so the rule is unit-testable, in the same shape as <see cref="ViuAutoClosingLogic"/>:
/// the decision is here and the Visual Studio adapter that acts on it holds no rule of its own.
/// </para>
/// <para>
/// <b>Tab belongs to the author, not to this feature.</b> It only means expansion when a bare word
/// sits immediately before the caret and that word is a shortcut this extension ships; every other
/// Tab indents, as it always did. The adapter adds one more condition it can see and this cannot:
/// no completion session may be open, because Tab commits one.
/// </para>
/// <para>
/// <b>Section matters.</b> The snippets are C# declarations, so they expand in a <c>@script</c>
/// block and nowhere else — inserting one into template markup or a style rule would write text that
/// cannot parse. That is the same section restriction the brace expansion carries and for the same
/// reason.
/// </para>
/// </remarks>
internal static class ViuSnippetShortcut
{
    // The shipped shortcuts, which is also the list a new .snippet file has to join. Kept ordinal:
    // a snippet shortcut is matched exactly, as the C# editor matches its own.
    private static readonly HashSet<string> Shortcuts =
        new(System.StringComparer.Ordinal) { "prop" };

    /// <summary>Gets the shortcuts this extension ships, in no particular order.</summary>
    public static IEnumerable<string> All => Shortcuts;

    /// <summary>
    /// Returns the shortcut a <c>Tab</c> at this position should expand, or <see langword="null"/>
    /// when the key means what it always means.
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="lineNumber">Zero-based line the caret is on.</param>
    /// <param name="characterIndex">Zero-based caret offset within that line.</param>
    /// <param name="start">The zero-based offset the shortcut begins at, when one is returned.</param>
    /// <returns>The shortcut, or <see langword="null"/>.</returns>
    public static string? Find(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex,
        out int start)
    {
        start = characterIndex;
        if (lines is null ||
            lineNumber < 0 ||
            lineNumber >= lines.Count)
        {
            return null;
        }

        string line = lines[lineNumber];
        if (characterIndex < 0 || characterIndex > line.Length)
        {
            return null;
        }

        int wordStart = characterIndex;
        while (wordStart > 0 && IsShortcutCharacter(line[wordStart - 1]))
        {
            wordStart--;
        }

        if (wordStart == characterIndex)
        {
            return null;
        }

        // A word continuing past the caret is one the author is still inside, not one they finished.
        if (characterIndex < line.Length && IsShortcutCharacter(line[characterIndex]))
        {
            return null;
        }

        string word = line.Substring(wordStart, characterIndex - wordStart);
        if (!Shortcuts.Contains(word) ||
            ViuSectionScanner.ScanLineSections(lines)[lineNumber] != ViuSectionKind.Script)
        {
            return null;
        }

        start = wordStart;
        return word;
    }

    private static bool IsShortcutCharacter(char character) =>
        char.IsLetterOrDigit(character) || character == '_';
}
