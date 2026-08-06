using System;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Classifies the syntactic context of a completion position inside an <c>@script</c> block
/// ([V01.01.12.07.04]) so each completion source is offered only where the C# grammar admits it.
/// </summary>
/// <remarks>
/// <para>
/// The classification is deliberately lexical rather than a second Roslyn parse: it runs on every
/// keystroke ahead of the semantic engine, and the semantic engine's own compilation is the
/// expensive step the language service already pays for once. A forward scan over the block content
/// tracks brace depth outside comments, string literals, and character literals, which is exactly
/// what separates a top-level <c>using</c> <em>directive</em> (depth 0) from a <c>using</c>
/// <em>statement</em> inside a method body (depth greater than 0).
/// </para>
/// <para>
/// Only the contexts the service can act on differently are distinguished. Everything that is not a
/// using directive is <see cref="ScriptCompletionContextKind.Expression"/>, including a
/// member-declaration position between members: a field initializer (<c>private int _total = _seed
/// + 1;</c>) begins on such a line, so the service cannot suppress instance members there without
/// also breaking the initializer — and the component's own members, private ones included, are
/// legitimately in scope for it.
/// </para>
/// </remarks>
internal static class ScriptCompletionContext
{
    /// <summary>
    /// Classifies the position <paramref name="contentOffset"/> within an <c>@script</c> block's
    /// content.
    /// </summary>
    /// <param name="blockContent">The script block's raw content text.</param>
    /// <param name="contentOffset">
    /// The zero-based cursor offset within <paramref name="blockContent"/>. An offset outside the
    /// content classifies as <see cref="ScriptCompletionContextKind.Expression"/>, the unchanged
    /// default.
    /// </param>
    /// <returns>The context kind that selects the applicable completion sources.</returns>
    internal static ScriptCompletionContextKind Classify(string blockContent, int contentOffset)
    {
        ArgumentNullException.ThrowIfNull(blockContent);

        if (contentOffset < 0 || contentOffset > blockContent.Length)
        {
            return ScriptCompletionContextKind.Expression;
        }

        if (!TryScan(blockContent, contentOffset, out var depth, out var lineStart) || depth != 0)
        {
            return ScriptCompletionContextKind.Expression;
        }

        return IsUsingDirectivePrefix(blockContent, lineStart, contentOffset)
            ? ScriptCompletionContextKind.UsingDirective
            : ScriptCompletionContextKind.Expression;
    }

    // The line prefix rule: optional leading whitespace, an optional `global` modifier, the `using`
    // keyword, and at least one character of whitespace after it — so `usin`/`using` still complete
    // as the keyword, while `using ` (and `using static `, `global using `, `using Alias = `) has
    // entered the directive's namespace-or-type name. A `;` already typed on the line closes the
    // directive and returns the position to the ordinary member context.
    private static bool IsUsingDirectivePrefix(string content, int lineStart, int offset)
    {
        var index = SkipWhitespace(content, lineStart, offset);
        if (Matches(content, index, offset, "global"))
        {
            var afterGlobal = index + "global".Length;
            if (afterGlobal >= offset || !IsInlineWhitespace(content[afterGlobal]))
            {
                return false;
            }

            index = SkipWhitespace(content, afterGlobal, offset);
        }

        if (!Matches(content, index, offset, "using"))
        {
            return false;
        }

        var afterUsing = index + "using".Length;
        if (afterUsing >= offset || !IsInlineWhitespace(content[afterUsing]))
        {
            return false;
        }

        return content.IndexOf(';', afterUsing, offset - afterUsing) < 0;
    }

    private static bool Matches(string content, int index, int limit, string keyword)
        => index + keyword.Length <= limit &&
           string.CompareOrdinal(content, index, keyword, 0, keyword.Length) == 0;

    private static int SkipWhitespace(string content, int index, int limit)
    {
        while (index < limit && IsInlineWhitespace(content[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsInlineWhitespace(char character)
        => character is ' ' or '\t';

    // Scans the content prefix, reporting the brace depth and current line start at the cursor.
    // Returns false when the cursor sits inside a comment or a literal, where no completion source
    // is context-sensitive and the caller keeps its unchanged default.
    private static bool TryScan(string content, int offset, out int depth, out int lineStart)
    {
        depth = 0;
        lineStart = 0;
        var index = 0;
        while (index < offset)
        {
            var character = content[index];
            switch (character)
            {
                case '\n':
                    index++;
                    lineStart = index;
                    continue;

                case '{':
                    depth++;
                    index++;
                    continue;

                case '}':
                    if (depth > 0)
                    {
                        depth--;
                    }

                    index++;
                    continue;

                case '/' when index + 1 < content.Length && content[index + 1] == '/':
                    index = SkipLineComment(content, index);
                    if (index > offset)
                    {
                        return false;
                    }

                    continue;

                case '/' when index + 1 < content.Length && content[index + 1] == '*':
                    if (!TrySkipBlockComment(content, ref index) || index > offset)
                    {
                        return false;
                    }

                    continue;

                case '"':
                case '\'':
                case '@' when index + 1 < content.Length && content[index + 1] == '"':
                case '$' when index + 1 < content.Length &&
                    content[index + 1] is '"' or '@' or '$':
                    if (!TrySkipLiteral(content, ref index, ref lineStart) || index > offset)
                    {
                        return false;
                    }

                    continue;

                default:
                    index++;
                    continue;
            }
        }

        return true;
    }

    private static int SkipLineComment(string content, int index)
    {
        var newLine = content.IndexOf('\n', index);
        return newLine < 0 ? content.Length : newLine;
    }

    private static bool TrySkipBlockComment(string content, ref int index)
    {
        var close = content.IndexOf("*/", index + 2, StringComparison.Ordinal);
        if (close < 0)
        {
            // An unterminated block comment swallows the cursor: report the scan as inconclusive.
            index = content.Length;
            return false;
        }

        index = close + 2;
        return true;
    }

    // Skips one literal: raw (`"""`), verbatim (`@"`), interpolated (any run of `$` and `@`
    // prefixes), regular, or character. Braces inside a literal — including an interpolation hole's
    // own braces, which are balanced within the literal — never move the depth, which is why the
    // whole literal is skipped as a unit.
    private static bool TrySkipLiteral(string content, ref int index, ref int lineStart)
    {
        var start = index;
        var isVerbatim = false;
        while (index < content.Length && content[index] is '$' or '@')
        {
            isVerbatim |= content[index] == '@';
            index++;
        }

        if (index >= content.Length)
        {
            return false;
        }

        var quote = content[index];
        if (quote is not ('"' or '\''))
        {
            // A bare `@` or `$` that introduces no literal (a verbatim identifier, most commonly):
            // resume the ordinary scan at the character after the prefix run.
            index = start + 1;
            return true;
        }

        if (quote == '"' && !isVerbatim && Matches(content, index, content.Length, "\"\"\""))
        {
            return TrySkipRawStringLiteral(content, ref index, ref lineStart);
        }

        index++;
        while (index < content.Length)
        {
            var character = content[index];
            if (character == '\\' && !isVerbatim)
            {
                index += 2;
                continue;
            }

            if (character == quote)
            {
                // A verbatim string escapes its quote by doubling it.
                if (isVerbatim && index + 1 < content.Length && content[index + 1] == quote)
                {
                    index += 2;
                    continue;
                }

                index++;
                return true;
            }

            if (character == '\n')
            {
                lineStart = index + 1;
                if (!isVerbatim)
                {
                    // A single-line literal never crosses a newline; Roslyn's own recovery ends it
                    // at the line break, and so does this scan.
                    index++;
                    return true;
                }
            }

            index++;
        }

        return false;
    }

    private static bool TrySkipRawStringLiteral(string content, ref int index, ref int lineStart)
    {
        var quoteCount = 0;
        while (index < content.Length && content[index] == '"')
        {
            quoteCount++;
            index++;
        }

        while (index < content.Length)
        {
            if (content[index] == '\n')
            {
                lineStart = index + 1;
                index++;
                continue;
            }

            if (content[index] != '"')
            {
                index++;
                continue;
            }

            var run = 0;
            while (index < content.Length && content[index] == '"')
            {
                run++;
                index++;
            }

            if (run >= quoteCount)
            {
                return true;
            }
        }

        return false;
    }
}
