using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityCandidateTextScanner
{
    public static List<UtilityCandidateTextSpan> Scan(
        string text,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        var spans = new List<UtilityCandidateTextSpan>();
        var index = start;

        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (index < end && char.IsWhiteSpace(text[index]))
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
            }

            if (index >= end)
            {
                break;
            }

            var tokenStart = index;
            var closingDelimiters = new List<char>();
            var quote = '\0';
            var isEscaped = false;

            while (index < end)
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

                if (TryGetClosingDelimiter(character, out var closingDelimiter))
                {
                    closingDelimiters.Add(closingDelimiter);
                    index++;
                    continue;
                }

                if (IsClosingDelimiter(character))
                {
                    if (closingDelimiters.Count != 0 &&
                        closingDelimiters[closingDelimiters.Count - 1] == character)
                    {
                        closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                    }

                    index++;
                    continue;
                }

                if (char.IsWhiteSpace(character))
                {
                    if (closingDelimiters.Count == 0 ||
                        !CanCloseAfterWhitespace(
                            text,
                            index + 1,
                            end,
                            closingDelimiters,
                            cancellationToken))
                    {
                        break;
                    }
                }

                index++;
            }

            spans.Add(
                new UtilityCandidateTextSpan(
                    tokenStart,
                    index - tokenStart));

            if (index == tokenStart)
            {
                index++;
            }
        }

        return spans;
    }

    public static UtilityCandidateTextSpan FindAtPosition(
        string text,
        int start,
        int end,
        int position,
        CancellationToken cancellationToken)
    {
        var spans = Scan(text, start, end, cancellationToken);
        foreach (var span in spans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (position > span.Start && position < span.End)
            {
                return span;
            }

            if (position == span.Start)
            {
                return span;
            }

            if (position == span.End &&
                position > span.Start &&
                !char.IsWhiteSpace(text[position - 1]))
            {
                return span;
            }
        }

        return new UtilityCandidateTextSpan(position, 0);
    }

    internal static bool CanCloseAfterWhitespace(
        string text,
        int start,
        int end,
        IReadOnlyList<char> currentClosingDelimiters,
        CancellationToken cancellationToken)
    {
        var closingDelimiters = new List<char>(currentClosingDelimiters.Count);
        for (var index = 0; index < currentClosingDelimiters.Count; index++)
        {
            closingDelimiters.Add(currentClosingDelimiters[index]);
        }

        var quote = '\0';
        var isEscaped = false;

        for (var index = start; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (TryGetClosingDelimiter(character, out var closingDelimiter))
            {
                closingDelimiters.Add(closingDelimiter);
                continue;
            }

            if (!IsClosingDelimiter(character))
            {
                continue;
            }

            if (closingDelimiters.Count == 0 ||
                closingDelimiters[closingDelimiters.Count - 1] != character)
            {
                return false;
            }

            closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
            if (closingDelimiters.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetClosingDelimiter(
        char character,
        out char closingDelimiter)
    {
        switch (character)
        {
            case '(':
                closingDelimiter = ')';
                return true;
            case '[':
                closingDelimiter = ']';
                return true;
            case '{':
                closingDelimiter = '}';
                return true;
            default:
                closingDelimiter = '\0';
                return false;
        }
    }

    internal static bool IsClosingDelimiter(char character) =>
        character is ')' or ']' or '}';
}
