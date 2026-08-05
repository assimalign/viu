using System;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Computes the indentation a <c>{ }</c> block takes when it is expanded onto three lines
/// ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// The <c>viu</c> content type has no <c>ISmartIndentProvider</c> — nothing in Visual Studio
/// registers one for it, and writing one would mean teaching the editor the whole container grammar
/// for the sake of a single keystroke. The block expansion therefore computes its own indentation
/// from two things it can always see: the opening brace line's leading whitespace, and the buffer's
/// own tab options. That is deterministic, needs no parse, and produces the same shape Visual
/// Studio's C# editor produces for an empty block.
/// </para>
/// <para>
/// <b>Recorded decision — existing whitespace is copied, never rewritten.</b> The closing brace
/// takes the opening line's leading whitespace exactly as authored, even when it disagrees with the
/// current tabs-versus-spaces option. A file indented with tabs opened under a spaces setting keeps
/// its tabs on the lines this feature writes; only the <em>added</em> level follows the option. The
/// alternative — normalizing the copied whitespace — would silently reformat a line the user did not
/// touch.
/// </para>
/// <para>
/// Pure and editor-free, so it is compiled into the test project through a
/// <c>&lt;Compile Include&gt;</c> link and unit-tested directly.
/// </para>
/// </remarks>
internal static class ViuBraceIndentation
{
    /// <summary>
    /// Computes the indentation for the closing brace's line and the caret's line.
    /// </summary>
    /// <param name="openingBraceLineText">
    /// The full text of the line the opening brace sits on, without its line break.
    /// </param>
    /// <param name="indentSize">
    /// The buffer's indent size, in spaces. Values below one are treated as one: the whole point of
    /// the expansion is to place the caret one level inside the block, and a zero-width level would
    /// leave a blank line indistinguishable from the closer's.
    /// </param>
    /// <param name="convertTabsToSpaces">
    /// Whether the buffer indents with spaces. When <see langword="false"/> the added level is a
    /// single tab character, whatever <paramref name="indentSize"/> says.
    /// </param>
    /// <returns>The two indentation strings, which are never <see langword="null"/>.</returns>
    public static ViuBlockExpansion ComputeBlockExpansion(
        string openingBraceLineText,
        int indentSize,
        bool convertTabsToSpaces)
    {
        string closingBraceIndentation = ReadLeadingWhitespace(openingBraceLineText);
        string indentLevel = convertTabsToSpaces
            ? new string(' ', Math.Max(indentSize, 1))
            : "\t";

        return new ViuBlockExpansion(
            closingBraceIndentation,
            closingBraceIndentation + indentLevel);
    }

    /// <summary>
    /// Reads the whitespace a line begins with.
    /// </summary>
    /// <param name="lineText">The line to read, without its line break.</param>
    /// <returns>
    /// The leading whitespace, verbatim; the empty string for a line that starts with a
    /// non-whitespace character, and the whole line for one that is entirely whitespace.
    /// </returns>
    public static string ReadLeadingWhitespace(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
        {
            return string.Empty;
        }

        int end = 0;
        while (end < lineText.Length && IsIndentationWhitespace(lineText[end]))
        {
            end++;
        }

        return end == 0 ? string.Empty : lineText.Substring(0, end);
    }

    // Only the two characters that can appear in a line's indentation. char.IsWhiteSpace would also
    // accept the line separators a line's text never contains, so the narrower test says exactly what
    // is meant.
    private static bool IsIndentationWhitespace(char character) => character is ' ' or '\t';
}
