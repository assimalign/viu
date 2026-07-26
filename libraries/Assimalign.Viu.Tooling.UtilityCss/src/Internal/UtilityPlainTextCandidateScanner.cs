using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityPlainTextCandidateScanner
{
    public static List<UtilityCandidateScanRegion> Scan(
        string text,
        CancellationToken cancellationToken)
    {
        var regions = new List<UtilityCandidateScanRegion>();
        var index = 0;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsStandaloneInterpolationStart(text, index))
            {
                index = SkipInterpolation(
                    text,
                    index,
                    cancellationToken);
                continue;
            }

            if (IsBoundary(text, index) ||
                !IsCandidateStart(text, index))
            {
                index++;
                continue;
            }

            var start = index;
            var closingDelimiters = new List<char>();
            var quote = '\0';
            var isEscaped = false;

            while (index < text.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var character = text[index];

                if (isEscaped)
                {
                    isEscaped = false;
                    index++;
                    continue;
                }

                if (character == '\\')
                {
                    isEscaped = true;
                    index++;
                    continue;
                }

                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }

                    index++;
                    continue;
                }

                if (closingDelimiters.Count != 0 &&
                    character is '\'' or '"')
                {
                    quote = character;
                    index++;
                    continue;
                }

                if (character == '{' &&
                    IsInlineInterpolationStart(text, start, index))
                {
                    closingDelimiters.Add('}');
                    if (index + 1 < text.Length &&
                        text[index + 1] == '{')
                    {
                        closingDelimiters.Add('}');
                        index++;
                    }

                    index++;
                    continue;
                }

                if (UtilityCandidateTextScanner.TryGetClosingDelimiter(
                        character,
                        out var closingDelimiter))
                {
                    closingDelimiters.Add(closingDelimiter);
                    index++;
                    continue;
                }

                if (UtilityCandidateTextScanner.IsClosingDelimiter(character))
                {
                    if (closingDelimiters.Count == 0 ||
                        closingDelimiters[closingDelimiters.Count - 1] != character)
                    {
                        break;
                    }

                    closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                    index++;
                    continue;
                }

                if (char.IsWhiteSpace(character))
                {
                    if (closingDelimiters.Count == 0 ||
                        !UtilityCandidateTextScanner.CanCloseAfterWhitespace(
                            text,
                            index + 1,
                            text.Length,
                            closingDelimiters,
                            cancellationToken))
                    {
                        break;
                    }

                    index++;
                    continue;
                }

                if (closingDelimiters.Count == 0 &&
                    IsCandidateTerminator(text, index))
                {
                    break;
                }

                index++;
            }

            if (index > start)
            {
                regions.Add(
                    new UtilityCandidateScanRegion(
                        start,
                        index,
                        HasConcatenationBefore(text, start),
                        HasConcatenationAfter(text, index)));
            }

            if (index == start)
            {
                index++;
            }
        }

        return regions;
    }

    private static bool IsCandidateStart(
        string text,
        int index)
    {
        var character = text[index];
        if (character == '[')
        {
            var next = index + 1;
            while (next < text.Length &&
                   char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            return next >= text.Length ||
                   text[next] is not ('\'' or '"' or '`' or '[' or '{');
        }

        return char.IsLetterOrDigit(character) ||
               character is '-' or '!' or '@' or '*';
    }

    private static bool IsBoundary(
        string text,
        int index)
    {
        var character = text[index];
        return char.IsWhiteSpace(character) ||
               character is '"' or '\'' or '`' or '<' or '>' or '=' or ',' or ';' or
                   '{' or '}' or '(' or ')' or ']' or '?' or '|' or '&' or '+';
    }

    private static bool IsCandidateTerminator(
        string text,
        int index)
    {
        var character = text[index];
        if (character == ':' &&
            index + 1 < text.Length &&
            char.IsWhiteSpace(text[index + 1]))
        {
            return true;
        }

        if (character == '.' &&
            (index + 1 >= text.Length ||
             !char.IsLetterOrDigit(text[index + 1])))
        {
            return true;
        }

        return character is '"' or '\'' or '`' or '<' or '>' or '=' or ',' or ';' or
            '{' or '}' or '?' or '|' or '&' or '+';
    }

    private static bool IsStandaloneInterpolationStart(
        string text,
        int index) =>
        (text[index] == '$' &&
         index + 1 < text.Length &&
         text[index + 1] == '{') ||
        (text[index] == '{' &&
         index + 1 < text.Length &&
         text[index + 1] == '{');

    private static bool IsInlineInterpolationStart(
        string text,
        int tokenStart,
        int index) =>
        index > tokenStart ||
        (index + 1 < text.Length &&
         text[index + 1] == '{');

    private static int SkipInterpolation(
        string text,
        int start,
        CancellationToken cancellationToken)
    {
        var isMustache =
            text[start] == '{' &&
            start + 1 < text.Length &&
            text[start + 1] == '{';
        var index = start + 2;
        var depth = isMustache ? 2 : 1;
        var quote = '\0';
        var isEscaped = false;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (isEscaped)
            {
                isEscaped = false;
                index++;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                index++;
                continue;
            }

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                index++;
                continue;
            }

            if (character is '\'' or '"' or '`')
            {
                quote = character;
                index++;
                continue;
            }

            if (character == '{')
            {
                depth++;
                index++;
                continue;
            }

            if (character == '}')
            {
                depth--;
                index++;
                if (depth == 0)
                {
                    return index;
                }

                continue;
            }

            index++;
        }

        return text.Length;
    }

    private static bool HasConcatenationBefore(
        string text,
        int start)
    {
        var index = start - 1;
        if (index < 0 ||
            text[index] is not ('\'' or '"' or '`'))
        {
            return false;
        }

        index--;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index >= 0 && text[index] == '+';
    }

    private static bool HasConcatenationAfter(
        string text,
        int end)
    {
        var index = end;
        if (index >= text.Length ||
            text[index] is not ('\'' or '"' or '`'))
        {
            return false;
        }

        index++;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index < text.Length && text[index] == '+';
    }
}
