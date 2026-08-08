using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu.ServerRenderer;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Decides, from document text and a caret position alone, what a <c>.viu</c> document should
/// auto-close ([V01.01.12.07.08]).
/// </summary>
/// <remarks>
/// <para>
/// Four questions, across the two mechanisms the extension uses. <see cref="AllowsBracketPair"/>,
/// <see cref="AllowsQuotePair"/>, and <see cref="AllowsBlockExpansionOnReturn"/> answer the editor's
/// brace-completion engine — may this pair start here, and does <c>Enter</c> between its halves
/// expand into a block — and <see cref="GetTypedCharacterCompletion"/> answers the typed-character
/// command handler: what text completes the element or comment the user just opened.
/// </para>
/// <para>
/// Both answers are section-aware and take their section attribution from
/// <see cref="ViuSectionScanner"/> rather than re-scanning the container grammar, and the void
/// element list comes from <see cref="DomKnowledgeData.VoidTags"/> — the same WHATWG table the
/// template compiler and the runtime use — rather than from a table written for the editor. Nothing
/// here touches a Visual Studio type, which is what lets the whole decision surface be unit-tested
/// through the source-linked test project while the MEF and commanding glue stays a thin adapter.
/// </para>
/// <para>
/// Scanning is whole-document per keystroke, which is affordable because of when it happens: only in
/// a <c>.viu</c> buffer, and only on the five characters that can trigger a completion
/// (<c>&gt;</c>, <c>/</c>, <c>-</c>, and the two quotes) plus a <c>Return</c> pressed inside an
/// already-open brace session. Two regular expressions per line, no allocation per line beyond the
/// result array. <see cref="AllowsBracketPair"/> does not scan at all — the three brackets pair
/// everywhere, so it answers from the caret's range alone.
/// </para>
/// </remarks>
internal static class ViuAutoClosingLogic
{
    private static readonly HashSet<string> VoidTagSet = BuildVoidTagSet();

    /// <summary>
    /// Determines whether a typed character can trigger an auto-closing completion at all.
    /// </summary>
    /// <param name="typedCharacter">The character the user typed.</param>
    /// <returns>
    /// <see langword="true"/> for the three characters
    /// <see cref="GetTypedCharacterCompletion"/> can act on, so the command handler can decline
    /// every other keystroke without reading the buffer.
    /// </returns>
    public static bool IsCompletionTriggerCharacter(char typedCharacter) =>
        typedCharacter is '>' or '/' or '-' or '{' or '}';

    /// <summary>
    /// Determines whether a bracket typed at the given position may be paired.
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="lineNumber">Zero-based line the bracket is being typed on.</param>
    /// <param name="characterIndex">Zero-based offset within that line where the bracket goes.</param>
    /// <param name="openingCharacter">The bracket character: <c>{</c>, <c>(</c>, or <c>[</c>.</param>
    /// <returns><see langword="true"/> when the pair may be completed.</returns>
    /// <remarks>
    /// <para>
    /// The three brackets pair in every section but one position: braces, parentheses, and square
    /// brackets are balanced constructs in template markup, in C#, and in CSS alike. Quotes are the
    /// pairs that want general gating, and <see cref="AllowsQuotePair"/> is where that lives.
    /// </para>
    /// <para>
    /// <b>The one exception is the second <c>{</c> of a template interpolation.</b> A pair whose
    /// session starts here could never compose <c>{{}}</c>, and worse, a session that <em>did</em>
    /// start would race the explicit scaffold that does. Declining is what hands the keystroke to
    /// <see cref="GetTypedCharacterCompletion"/> cleanly: with no session pending, nothing else
    /// writes to the buffer for that character. See
    /// <see cref="GetInterpolationScaffold"/> for the shape it writes and why the session path cannot
    /// produce it.
    /// </para>
    /// <para>
    /// It is a decision rather than metadata because the brackets carry a brace-completion
    /// <em>context</em> ([V01.01.12.07.09], for <see cref="AllowsBlockExpansionOnReturn"/>), and a
    /// context provider is asked whether to start. Keeping the answer here — pure, with the document
    /// and caret in hand — pins the shipped matrix in a unit test and gives every position rule one
    /// place to land.
    /// </para>
    /// </remarks>
    public static bool AllowsBracketPair(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex,
        char openingCharacter)
    {
        if (openingCharacter is not ('{' or '(' or '['))
        {
            return false;
        }

        if (!IsPositionInRange(lines, lineNumber, characterIndex))
        {
            return false;
        }

        // Only the interpolation position declines, and only for a brace, so the section scan is
        // paid for exactly when a '{' follows a '{'.
        return openingCharacter != '{' ||
            !IsAfterOpeningBrace(lines[lineNumber], characterIndex) ||
            ViuSectionScanner.ScanLineSections(lines)[lineNumber] != ViuSectionKind.Template;
    }

    /// <summary>
    /// Computes the interpolation scaffold a second <c>{</c> writes in a template
    /// ([V01.01.12.07.09]).
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="lineNumber">Zero-based line the brace is being typed on.</param>
    /// <param name="characterIndex">Zero-based offset within that line where the brace goes.</param>
    /// <returns>
    /// The insertion that leaves <c>{{|}}</c> around the caret, or <see langword="null"/> when this
    /// is not the second brace of a template interpolation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Why this is explicit rather than a consequence of pairing.</b> [V01.01.12.07.08] claimed
    /// typing <c>{</c> twice yielded <c>{{}}</c> through the brace-completion engine. That was
    /// aspirational and is false: the editor's <c>ShouldStartSession</c> declines whenever the first
    /// non-whitespace character after the caret is a letter or digit, the pair's own characters, or
    /// <em>any registered opening brace</em> — and <c>&lt;</c> is one, so in the ordinary case of
    /// typing an interpolation inside an element (<c>&lt;p&gt;{|&lt;/p&gt;</c>) even the <em>first</em>
    /// brace never pairs. The scaffold therefore has to be written, not inherited.
    /// </para>
    /// <para>
    /// <b>Two caret states, one result.</b> If the first brace did pair, the caret sits at
    /// <c>{|}</c> and the scaffold inserts <c>{}</c> — the existing closer becomes the inner one. If
    /// it did not, the caret sits at <c>{|</c> with arbitrary text after it and the scaffold inserts
    /// <c>{}}</c>. Both leave <c>{{|}}</c>, so the user cannot tell which path they were on.
    /// </para>
    /// <para>
    /// <b>Recorded decision — no inner spaces.</b> The scaffold writes <c>{{}}</c> rather than
    /// <c>{{  }}</c>, matching what the Visual Studio Code client already produces from its
    /// <c>language-configuration.json</c> pair. An interpolation reads perfectly well as
    /// <c>{{ Count }}</c> and typing that space is one keystroke, whereas deleting two unwanted ones
    /// is two — and the spaces are not part of the syntax.
    /// </para>
    /// <para>
    /// Template-only, for the same reason every other element behavior is: in <c>@script</c> a
    /// second <c>{</c> is a nested C# block or an interpolated-string brace and must pair
    /// independently, and in <c>&lt;style&gt;</c> it is a nested at-rule block.
    /// </para>
    /// </remarks>
    public static ViuAutoClosingEdit? GetInterpolationScaffold(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex)
    {
        if (!IsPositionInRange(lines, lineNumber, characterIndex) ||
            !IsAfterOpeningBrace(lines[lineNumber], characterIndex))
        {
            return null;
        }

        string line = lines[lineNumber];
        bool closerFollows = characterIndex < line.Length && line[characterIndex] == '}';

        // "{}" reuses the closer the first brace's session already inserted; "{}}" supplies both when
        // it never started. The caret lands after the typed brace either way.
        return new ViuAutoClosingEdit(closerFollows ? "{}" : "{}}", 1);
    }

    /// <summary>
    /// Determines whether a <c>}</c> typed in a template should walk over the one already at the
    /// caret instead of inserting a second ([V01.01.12.07.09]).
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="lineNumber">Zero-based line the brace is being typed on.</param>
    /// <param name="characterIndex">Zero-based offset within that line where the brace would go.</param>
    /// <returns><see langword="true"/> when the caret should advance over an existing <c>}</c>.</returns>
    /// <remarks>
    /// <para>
    /// The scaffold is written by hand rather than by a brace-completion session, so nothing tracks
    /// it and the editor's own type-through does not apply: closing <c>{{}}</c> by typing the two
    /// braces would otherwise leave <c>{{}|}}</c>. This supplies the walk-over the missing session
    /// would have given.
    /// </para>
    /// <para>
    /// <b>Deliberately broader than interpolations.</b> The test is only "a <c>}</c> is already at
    /// the caret, in a template", not "the caret is inside an interpolation" — matching a closer to
    /// its opener would mean parsing the template to decide a keystroke. The cost is that a literal
    /// <c>}}}</c> in template prose cannot be typed straight through; it can still be pasted, or
    /// typed with an arrow key between the braces. Template text containing runs of closing braces is
    /// rare, and every one of them is a keystroke away from working.
    /// </para>
    /// <para>
    /// Script and style sections are untouched: there the platform's own session owns <c>}</c>, and
    /// a walk-over that fired outside a session would break ordinary C# and CSS authoring.
    /// </para>
    /// </remarks>
    public static bool AllowsClosingBraceWalkover(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex)
        => IsPositionInRange(lines, lineNumber, characterIndex) &&
           characterIndex < lines[lineNumber].Length &&
           lines[lineNumber][characterIndex] == '}' &&
           ViuSectionScanner.ScanLineSections(lines)[lineNumber] == ViuSectionKind.Template;

    // True when the character immediately before the caret is an opening brace. A caret at column
    // zero has no preceding character on its line, and a brace ending the line above opens nothing
    // an interpolation could continue - '{{' is a single token that never spans a line break.
    private static bool IsAfterOpeningBrace(string line, int characterIndex)
        => characterIndex > 0 &&
           characterIndex <= line.Length &&
           line[characterIndex - 1] == '{';

    /// <summary>
    /// Determines whether pressing <c>Enter</c> between an auto-completed <c>{</c> and <c>}</c>
    /// should expand them into an indented block ([V01.01.12.07.09]).
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="openingBraceLineNumber">Zero-based line the opening brace sits on.</param>
    /// <returns>
    /// <see langword="true"/> only for a brace opened in a script section, where the pair is a C#
    /// block and Visual Studio's C# editor is the behavior being matched.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The section restriction is load-bearing rather than conservative. In a template the paired
    /// braces are most often an interpolation — <c>{{ Count }}</c> is authored by typing <c>{</c>
    /// twice — and pushing its closer onto a line of its own would break the expression outright. A
    /// <c>&lt;style&gt;</c> rule really is a block, so expanding there would be defensible; it is
    /// deliberately left alone so this change carries exactly the behavior the work item specifies
    /// and template and style sections keep the pairing they shipped with. Recorded in
    /// <c>docs/DESIGN.md</c>; revisit if CSS authoring asks for it.
    /// </para>
    /// <para>
    /// The <em>opening</em> brace's line decides, not the caret's. By the time the editor asks, the
    /// caret is already on a line the Return created, and the block belongs to the construct that
    /// opened it.
    /// </para>
    /// </remarks>
    public static bool AllowsBlockExpansionOnReturn(
        IReadOnlyList<string> lines,
        int openingBraceLineNumber)
        => openingBraceLineNumber >= 0 &&
           openingBraceLineNumber < lines.Count &&
           ViuSectionScanner.ScanLineSections(lines)[openingBraceLineNumber] == ViuSectionKind.Script;

    /// <summary>
    /// Determines whether a quote typed at the given position means the start of a string, and may
    /// therefore be paired.
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="lineNumber">Zero-based line the quote is being typed on.</param>
    /// <param name="characterIndex">Zero-based offset within that line where the quote goes.</param>
    /// <param name="quoteCharacter">The quote character: <c>"</c> or <c>'</c>.</param>
    /// <returns><see langword="true"/> when the pair may be completed.</returns>
    /// <remarks>
    /// The gating exists because a quote is only sometimes a delimiter. In a script section every
    /// quote opens a C# string or character literal, so both forms pair. In a template only the
    /// attribute-value position is a string position: pairing a double quote in template text would
    /// interrupt ordinary prose, and pairing an apostrophe there would corrupt it outright, which is
    /// why the single quote never pairs outside script. Style sections and the text between
    /// containers pair neither.
    /// </remarks>
    public static bool AllowsQuotePair(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex,
        char quoteCharacter)
    {
        if (quoteCharacter is not ('"' or '\'') ||
            !IsPositionInRange(lines, lineNumber, characterIndex))
        {
            return false;
        }

        ViuSectionKind[] lineSections = ViuSectionScanner.ScanLineSections(lines);

        return lineSections[lineNumber] switch
        {
            // Every quote in a script section delimits a C# string or character literal.
            ViuSectionKind.Script => true,

            // In a template only an attribute value is a string, and only the double quote spells
            // one in the form Viu's own lexer recognizes.
            ViuSectionKind.Template => quoteCharacter == '"' &&
                IsAttributeValuePosition(lines, lineSections, lineNumber, characterIndex),

            _ => false,
        };
    }

    /// <summary>
    /// Computes the completion a typed character should produce, or <see langword="null"/> when it
    /// should be typed as an ordinary character.
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <param name="lineNumber">Zero-based line the character is being typed on.</param>
    /// <param name="characterIndex">Zero-based offset within that line where the character goes.</param>
    /// <param name="typedCharacter">The character the user typed.</param>
    /// <returns>The insertion and caret placement, or <see langword="null"/> for no completion.</returns>
    /// <remarks>
    /// <para>
    /// Four completions, all confined to template sections. <c>&gt;</c> after an open tag inserts
    /// that tag's end tag and leaves the caret between the two. <c>/</c> immediately after <c>&lt;</c>
    /// completes the nearest unclosed ancestor element, closing brace included. <c>-</c> completing
    /// <c>&lt;!--</c> inserts the comment terminator. <c>{</c> after a <c>{</c> writes the
    /// interpolation scaffold.
    /// </para>
    /// <para>
    /// The template restriction is the whole reason this is section-aware: <c>&gt;</c> in a
    /// <c>@script</c> block closes a generic argument list or a lambda arrow, and a script or style
    /// section that grew an end tag on every <c>&gt;</c> would be unusable.
    /// </para>
    /// </remarks>
    public static ViuAutoClosingEdit? GetTypedCharacterCompletion(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex,
        char typedCharacter)
    {
        if (!IsCompletionTriggerCharacter(typedCharacter) ||
            !IsPositionInRange(lines, lineNumber, characterIndex))
        {
            return null;
        }

        ViuSectionKind[] lineSections = ViuSectionScanner.ScanLineSections(lines);
        if (lineSections[lineNumber] != ViuSectionKind.Template)
        {
            return null;
        }

        return typedCharacter switch
        {
            '>' => GetEndTagCompletion(lines, lineSections, lineNumber, characterIndex),
            '/' => GetClosingTagCompletion(lines, lineSections, lineNumber, characterIndex),
            '-' => GetCommentCompletion(lines, lineNumber, characterIndex),
            '{' => GetInterpolationScaffold(lines, lineNumber, characterIndex),

            // '}' inserts nothing; it walks over an existing closer, which is a caret move rather
            // than an edit and is answered by AllowsClosingBraceWalkover.
            _ => null,
        };
    }

    // '>' finishing an open tag: insert the matching end tag and keep the caret between the two.
    private static ViuAutoClosingEdit? GetEndTagCompletion(
        IReadOnlyList<string> lines,
        ViuSectionKind[] lineSections,
        int lineNumber,
        int characterIndex)
    {
        if (!TryFindOpenTagStart(
                lines,
                lineSections,
                lineNumber,
                characterIndex,
                out int tagLineNumber,
                out int tagCharacterIndex))
        {
            return null;
        }

        string tagHeader = ReadTagHeaderText(
            lines,
            tagLineNumber,
            tagCharacterIndex,
            lineNumber,
            characterIndex);

        // '</', '<!', '<?', and a bare '<' name nothing to close.
        int tagNameLength = ReadTagNameLength(tagHeader, 1);
        if (tagNameLength == 0)
        {
            return null;
        }

        // A '>' inside an attribute value is content, not the end of the header.
        if (FindUnterminatedQuote(tagHeader) != '\0')
        {
            return null;
        }

        // A tag the user closed themselves takes no end tag.
        if (EndsWithSolidus(tagHeader))
        {
            return null;
        }

        string tagName = tagHeader.Substring(1, tagNameLength);
        if (VoidTagSet.Contains(tagName))
        {
            return null;
        }

        // Already closed: the end tag sits immediately after the caret, which is what re-typing the
        // '>' of an existing tag looks like.
        string endTag = "</" + tagName + ">";
        if (StartsWithAt(lines[lineNumber], characterIndex, endTag))
        {
            return null;
        }

        return new ViuAutoClosingEdit(">" + endTag, 1);
    }

    // '/' immediately after '<': complete the nearest unclosed ancestor element, '>' included.
    private static ViuAutoClosingEdit? GetClosingTagCompletion(
        IReadOnlyList<string> lines,
        ViuSectionKind[] lineSections,
        int lineNumber,
        int characterIndex)
    {
        string line = lines[lineNumber];
        if (characterIndex == 0 || line[characterIndex - 1] != '<')
        {
            return null;
        }

        string? ancestorName = FindNearestUnclosedElementName(
            lines,
            lineSections,
            lineNumber,
            characterIndex - 1);
        if (ancestorName is null)
        {
            return null;
        }

        string insertedText = "/" + ancestorName + ">";
        return new ViuAutoClosingEdit(insertedText, insertedText.Length);
    }

    // '-' completing '<!--': insert the terminator and park the caret in the middle of the comment.
    private static ViuAutoClosingEdit? GetCommentCompletion(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex)
    {
        string line = lines[lineNumber];
        if (characterIndex < 3 ||
            line[characterIndex - 3] != '<' ||
            line[characterIndex - 2] != '!' ||
            line[characterIndex - 1] != '-')
        {
            return null;
        }

        // A '-' already follows, so the user is editing an existing delimiter rather than opening a
        // comment; completing here would produce '<!--  ---->'.
        if (characterIndex < line.Length && line[characterIndex] == '-')
        {
            return null;
        }

        // Recorded shape ([V01.01.12.07.08]): the insertion is "-  -->" with the caret between the
        // two spaces, so the comment reads "<!-- text -->" the moment the user types - the spaced
        // form WHATWG comments are conventionally written in. Placing the caret hard against the
        // opener instead would leave "<!--text -->".
        return new ViuAutoClosingEdit("-  -->", 2);
    }

    // True when the caret sits where an attribute's value belongs: inside an open tag header, with
    // '=' as the last non-whitespace character before it, and not already inside a quoted value.
    private static bool IsAttributeValuePosition(
        IReadOnlyList<string> lines,
        ViuSectionKind[] lineSections,
        int lineNumber,
        int characterIndex)
    {
        if (!TryFindOpenTagStart(
                lines,
                lineSections,
                lineNumber,
                characterIndex,
                out int tagLineNumber,
                out int tagCharacterIndex))
        {
            return false;
        }

        string tagHeader = ReadTagHeaderText(
            lines,
            tagLineNumber,
            tagCharacterIndex,
            lineNumber,
            characterIndex);

        if (FindUnterminatedQuote(tagHeader) != '\0')
        {
            return false;
        }

        for (int index = tagHeader.Length - 1; index >= 0; index--)
        {
            char character = tagHeader[index];
            if (!char.IsWhiteSpace(character))
            {
                return character == '=';
            }
        }

        return false;
    }

    // Walks backwards for the '<' that opens the tag header the caret is inside, staying within the
    // template run the caret line belongs to. A '>' encountered first means the caret is in content
    // rather than in a header, which is what keeps template text and interpolation interiors out.
    private static bool TryFindOpenTagStart(
        IReadOnlyList<string> lines,
        ViuSectionKind[] lineSections,
        int lineNumber,
        int characterIndex,
        out int tagLineNumber,
        out int tagCharacterIndex)
    {
        for (int line = lineNumber; line >= 0 && lineSections[line] == ViuSectionKind.Template; line--)
        {
            string text = lines[line];
            int start = line == lineNumber ? characterIndex - 1 : text.Length - 1;
            for (int index = start; index >= 0; index--)
            {
                if (text[index] == '>')
                {
                    tagLineNumber = -1;
                    tagCharacterIndex = -1;
                    return false;
                }

                if (text[index] == '<')
                {
                    tagLineNumber = line;
                    tagCharacterIndex = index;
                    return true;
                }
            }
        }

        tagLineNumber = -1;
        tagCharacterIndex = -1;
        return false;
    }

    // The text of a tag header from its '<' up to (but excluding) the caret, with line breaks
    // normalized to '\n' - only the characters matter to the callers, never the offsets.
    private static string ReadTagHeaderText(
        IReadOnlyList<string> lines,
        int tagLineNumber,
        int tagCharacterIndex,
        int lineNumber,
        int characterIndex)
    {
        if (tagLineNumber == lineNumber)
        {
            return lines[lineNumber].Substring(
                tagCharacterIndex,
                characterIndex - tagCharacterIndex);
        }

        StringBuilder builder = new();
        string firstLine = lines[tagLineNumber];
        builder.Append(firstLine, tagCharacterIndex, firstLine.Length - tagCharacterIndex);
        for (int line = tagLineNumber + 1; line < lineNumber; line++)
        {
            builder.Append('\n').Append(lines[line]);
        }

        builder.Append('\n').Append(lines[lineNumber], 0, characterIndex);
        return builder.ToString();
    }

    // The nearest element still open at the given position, or null when nothing is open. The scan
    // starts at the top of the contiguous template run so nesting is counted from the section's own
    // beginning rather than from an arbitrary window.
    private static string? FindNearestUnclosedElementName(
        IReadOnlyList<string> lines,
        ViuSectionKind[] lineSections,
        int lineNumber,
        int characterIndex)
    {
        int runStart = lineNumber;
        while (runStart > 0 && lineSections[runStart - 1] == ViuSectionKind.Template)
        {
            runStart--;
        }

        StringBuilder builder = new();
        for (int line = runStart; line < lineNumber; line++)
        {
            builder.Append(lines[line]).Append('\n');
        }

        builder.Append(lines[lineNumber], 0, characterIndex);
        return ScanForNearestUnclosedElement(builder.ToString());
    }

    private static string? ScanForNearestUnclosedElement(string text)
    {
        List<string> openElements = [];
        int index = 0;

        while (index < text.Length)
        {
            if (text[index] != '<')
            {
                index++;
                continue;
            }

            if (StartsWithAt(text, index, "<!--"))
            {
                int commentEnd = text.IndexOf("-->", index + 4, StringComparison.Ordinal);
                index = commentEnd < 0 ? text.Length : commentEnd + 3;
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '/')
            {
                int closingNameLength = ReadTagNameLength(text, index + 2);
                if (closingNameLength > 0)
                {
                    // Ordinal, because Viu resolves a tag name by its authored spelling
                    // (specified by [CMP-6]). An end tag that matches an ancestor closes it and
                    // everything opened since; one that matches nothing is left alone.
                    string closingName = text.Substring(index + 2, closingNameLength);
                    int matchIndex = openElements.LastIndexOf(closingName);
                    if (matchIndex >= 0)
                    {
                        openElements.RemoveRange(matchIndex, openElements.Count - matchIndex);
                    }
                }

                index = SkipTagHeader(text, index, out _);
                continue;
            }

            int tagNameLength = ReadTagNameLength(text, index + 1);
            if (tagNameLength == 0)
            {
                index++;
                continue;
            }

            string tagName = text.Substring(index + 1, tagNameLength);
            index = SkipTagHeader(text, index, out bool isSelfClosing);
            if (!isSelfClosing && !VoidTagSet.Contains(tagName))
            {
                openElements.Add(tagName);
            }
        }

        return openElements.Count > 0 ? openElements[openElements.Count - 1] : null;
    }

    // Advances past a tag header that starts at '<', treating quoted attribute values as opaque.
    // Returns the index just past the header's '>', or the end of the text for an unterminated one.
    private static int SkipTagHeader(string text, int start, out bool isSelfClosing)
    {
        isSelfClosing = false;
        char openQuote = '\0';

        for (int index = start + 1; index < text.Length; index++)
        {
            char character = text[index];
            if (openQuote != '\0')
            {
                if (character == openQuote)
                {
                    openQuote = '\0';
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                openQuote = character;
                continue;
            }

            if (character == '>')
            {
                int previous = index - 1;
                while (previous > start && char.IsWhiteSpace(text[previous]))
                {
                    previous--;
                }

                isSelfClosing = previous > start && text[previous] == '/';
                return index + 1;
            }
        }

        return text.Length;
    }

    // The quote character an attribute value opened with and never closed, or '\0' when every quote
    // in the text is balanced. A quote of the other form inside an open value is content, which is
    // what keeps :title="it's fine" from reading as an unterminated value.
    private static char FindUnterminatedQuote(string text)
    {
        char openQuote = '\0';
        foreach (char character in text)
        {
            if (openQuote == '\0')
            {
                if (character is '"' or '\'')
                {
                    openQuote = character;
                }
            }
            else if (character == openQuote)
            {
                openQuote = '\0';
            }
        }

        return openQuote;
    }

    private static bool EndsWithSolidus(string text)
    {
        for (int index = text.Length - 1; index >= 0; index--)
        {
            char character = text[index];
            if (!char.IsWhiteSpace(character))
            {
                return character == '/';
            }
        }

        return false;
    }

    private static int ReadTagNameLength(string text, int start)
    {
        if (start >= text.Length || !IsTagNameStartCharacter(text[start]))
        {
            return 0;
        }

        int end = start + 1;
        while (end < text.Length && IsTagNameCharacter(text[end]))
        {
            end++;
        }

        return end - start;
    }

    private static bool IsTagNameStartCharacter(char character)
        => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool IsTagNameCharacter(char character)
        => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
            or '_' or '.' or ':' or '-';

    private static bool StartsWithAt(string text, int index, string value)
        => index >= 0 &&
           index + value.Length <= text.Length &&
           string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    private static bool IsPositionInRange(
        IReadOnlyList<string> lines,
        int lineNumber,
        int characterIndex)
        => lineNumber >= 0 &&
           lineNumber < lines.Count &&
           characterIndex >= 0 &&
           characterIndex <= lines[lineNumber].Length;

    // The WHATWG void elements, from the repository's single authoritative DOM knowledge table.
    // Case-insensitive to match how the template compiler and the runtime resolve them.
    private static HashSet<string> BuildVoidTagSet()
    {
        HashSet<string> voidTags = new(StringComparer.OrdinalIgnoreCase);
        foreach (string voidTag in DomKnowledgeData.VoidTags.Split(','))
        {
            voidTags.Add(voidTag);
        }

        return voidTags;
    }
}
