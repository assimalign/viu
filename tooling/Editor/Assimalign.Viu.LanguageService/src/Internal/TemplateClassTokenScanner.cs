using System;
using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Finds literal class tokens and attribute-value positions in live template content.
/// </summary>
internal static class TemplateClassTokenScanner
{
    internal static TemplateClassTokenContext? FindTokenAtPosition(
        string? templateText,
        int templateStart,
        int documentPosition)
    {
        if (string.IsNullOrEmpty(templateText) ||
            templateStart < 0 ||
            documentPosition < templateStart)
        {
            return null;
        }

        var position = documentPosition - templateStart;
        if (position > templateText!.Length ||
            !TryFindAttributeValue(templateText, position, out var attribute))
        {
            return null;
        }

        ClassRegion region;
        if (string.Equals(attribute.Name, "class", StringComparison.OrdinalIgnoreCase))
        {
            region = new ClassRegion(attribute.Start, attribute.End, false, false);
        }
        else if ((string.Equals(attribute.Name, ":class", StringComparison.Ordinal) ||
                  string.Equals(attribute.Name, "v-bind:class", StringComparison.Ordinal)) &&
                 TryFindBindingRegion(
                     templateText,
                     attribute.Start,
                     attribute.End,
                     position,
                     out var bindingRegion))
        {
            region = bindingRegion;
        }
        else
        {
            return null;
        }

        var token = FindTokenSpan(templateText, region.Start, region.End, position);
        if ((region.RejectsStartBoundary && token.Start == region.Start) ||
            (region.RejectsEndBoundary && token.End == region.End) ||
            ContainsDynamicInterpolation(
                templateText.Substring(token.Start, token.Length)))
        {
            return null;
        }

        var prefixLength = Math.Max(0, Math.Min(token.Length, position - token.Start));
        return new TemplateClassTokenContext(
            checked(templateStart + token.Start),
            checked(templateStart + token.End),
            templateText.Substring(token.Start, token.Length),
            templateText.Substring(token.Start, prefixLength));
    }

    internal static bool IsInsideAttributeValue(string? templateText, int position)
        => !string.IsNullOrEmpty(templateText) &&
           position >= 0 &&
           position <= templateText!.Length &&
           TryFindAttributeValue(templateText, position, out _);

    private static bool TryFindAttributeValue(
        string templateText,
        int position,
        out AttributeValue value)
    {
        value = default;
        TemplateCompletionProvider.AnalyzeMarkupPrefix(
            templateText,
            position,
            out var tagStart,
            out _,
            out var isComment,
            recognizeInterpolations: false,
            recognizeEscapedQuotes: true);
        if (isComment ||
            tagStart < 0 ||
            !TemplateCompletionProvider.TryReadTagContext(
                templateText,
                tagStart,
                position,
                out var tagContext,
                recognizeEscapedQuotes: true) ||
            !tagContext.IsAttributeValue ||
            tagContext.AttributeName.Length == 0 ||
            tagContext.AttributeValueStart < 0 ||
            position < tagContext.AttributeValueStart ||
            position > tagContext.AttributeValueEnd)
        {
            return false;
        }

        value = new AttributeValue(
            tagContext.AttributeName,
            tagContext.AttributeValueStart,
            tagContext.AttributeValueEnd);
        return true;
    }

    private static bool TryFindBindingRegion(
        string templateText,
        int start,
        int end,
        int position,
        out ClassRegion region)
    {
        region = default;
        var closingDelimiters = new List<char>();
        var index = start;
        while (index < end)
        {
            var character = templateText[index];
            if (character is '\'' or '"' or '`')
            {
                var quoteStart = index;
                var literalStart = index + 1;
                var literalEnd = FindClosingQuote(
                    templateText,
                    literalStart,
                    end,
                    character);
                var hasClosingQuote = literalEnd < end;
                var afterLiteral = hasClosingQuote ? literalEnd + 1 : literalEnd;
                if (position >= literalStart && position <= literalEnd)
                {
                    region = new ClassRegion(
                        literalStart,
                        literalEnd,
                        HasAdjacentPlusBefore(templateText, start, quoteStart),
                        HasAdjacentPlusAfter(templateText, afterLiteral, end));
                    return true;
                }

                if (!hasClosingQuote)
                {
                    return false;
                }

                index = afterLiteral;
                continue;
            }

            var closingDelimiter = GetClosingDelimiter(character);
            if (closingDelimiter != '\0')
            {
                closingDelimiters.Add(closingDelimiter);
            }
            else if (IsClosingDelimiter(character))
            {
                PopMatchingDelimiter(closingDelimiters, character);
            }
            else if (character == ':' &&
                     closingDelimiters.Count != 0 &&
                     closingDelimiters[closingDelimiters.Count - 1] == '}' &&
                     TryGetObjectKey(templateText, start, index, out var key) &&
                     position >= key.Start && position <= key.End)
            {
                region = new ClassRegion(key.Start, key.End, false, false);
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool TryGetObjectKey(
        string templateText,
        int expressionStart,
        int colon,
        out TextSpan key)
    {
        var start = colon - 1;
        while (start >= expressionStart && templateText[start] is not ('{' or ','))
        {
            start--;
        }

        start++;
        var end = colon;
        while (start < end && char.IsWhiteSpace(templateText[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(templateText[end - 1]))
        {
            end--;
        }

        for (var index = start; index < end; index++)
        {
            if (char.IsWhiteSpace(templateText[index]) ||
                templateText[index] is '?' or '\'' or '"' or '`' or '[' or ']' or '(' or ')')
            {
                key = default;
                return false;
            }
        }

        key = new TextSpan(start, end - start);
        return start != end;
    }

    private static TextSpan FindTokenSpan(
        string templateText,
        int start,
        int end,
        int position)
    {
        var index = start;
        while (index < end)
        {
            while (index < end && char.IsWhiteSpace(templateText[index]))
            {
                index++;
            }

            if (index == end)
            {
                break;
            }

            var tokenStart = index;
            var closingDelimiters = new List<char>();
            var quote = '\0';
            var isEscaped = false;
            while (index < end)
            {
                var character = templateText[index];
                if (ConsumeQuotedCharacter(character, ref quote, ref isEscaped))
                {
                    index++;
                    continue;
                }

                if (closingDelimiters.Count != 0 && character is '\'' or '"')
                {
                    quote = character;
                }
                else
                {
                    var closingDelimiter = GetClosingDelimiter(character);
                    if (closingDelimiter != '\0')
                    {
                        closingDelimiters.Add(closingDelimiter);
                    }
                    else if (IsClosingDelimiter(character))
                    {
                        PopMatchingDelimiter(closingDelimiters, character);
                    }
                    else if (char.IsWhiteSpace(character) &&
                             (closingDelimiters.Count == 0 ||
                              !CanCloseAfterWhitespace(
                                  templateText,
                                  index + 1,
                                  end,
                                  closingDelimiters)))
                    {
                        break;
                    }
                }

                index++;
            }

            var token = new TextSpan(tokenStart, index - tokenStart);
            if ((position >= token.Start && position < token.End) ||
                (position == token.End &&
                 position > token.Start &&
                 !char.IsWhiteSpace(templateText[position - 1])))
            {
                return token;
            }
        }

        return new TextSpan(position, 0);
    }

    private static bool CanCloseAfterWhitespace(
        string templateText,
        int start,
        int end,
        IReadOnlyList<char> currentClosingDelimiters)
    {
        var closingDelimiters = new List<char>(currentClosingDelimiters);
        var quote = '\0';
        var isEscaped = false;
        for (var index = start; index < end; index++)
        {
            var character = templateText[index];
            if (ConsumeQuotedCharacter(character, ref quote, ref isEscaped))
            {
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            var closingDelimiter = GetClosingDelimiter(character);
            if (closingDelimiter != '\0')
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

    private static bool ContainsDynamicInterpolation(string tokenText)
    {
        if (tokenText.Contains("${", StringComparison.Ordinal) ||
            tokenText.Contains("{{", StringComparison.Ordinal) ||
            tokenText.Contains("}}", StringComparison.Ordinal))
        {
            return true;
        }

        var closingDelimiters = new List<char>();
        var quote = '\0';
        var isEscaped = false;
        foreach (var character in tokenText)
        {
            if (ConsumeQuotedCharacter(character, ref quote, ref isEscaped))
            {
                continue;
            }

            if (closingDelimiters.Count != 0 && character is '\'' or '"')
            {
                quote = character;
            }
            else if (character is '[' or '(')
            {
                closingDelimiters.Add(character == '[' ? ']' : ')');
            }
            else if (character is ']' or ')')
            {
                PopMatchingDelimiter(closingDelimiters, character);
            }
            else if (character == '{' && closingDelimiters.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConsumeQuotedCharacter(
        char character,
        ref char quote,
        ref bool isEscaped)
    {
        if (isEscaped)
        {
            isEscaped = false;
            return true;
        }

        if (character == '\\')
        {
            isEscaped = true;
            return true;
        }

        if (quote == '\0')
        {
            return false;
        }

        if (character == quote)
        {
            quote = '\0';
        }

        return true;
    }

    private static int FindClosingQuote(
        string templateText,
        int start,
        int end,
        char quote)
    {
        var isEscaped = false;
        for (var index = start; index < end; index++)
        {
            if (isEscaped)
            {
                isEscaped = false;
            }
            else if (templateText[index] == '\\')
            {
                isEscaped = true;
            }
            else if (templateText[index] == quote)
            {
                return index;
            }
        }

        return end;
    }

    private static bool HasAdjacentPlusBefore(string text, int start, int quoteStart)
    {
        var index = quoteStart - 1;
        while (index >= start && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index >= start && text[index] == '+';
    }

    private static bool HasAdjacentPlusAfter(string text, int afterQuote, int end)
    {
        var index = afterQuote;
        while (index < end && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index < end && text[index] == '+';
    }

    private static char GetClosingDelimiter(char character)
        => character switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            _ => '\0',
        };

    private static bool IsClosingDelimiter(char character)
        => character is ')' or ']' or '}';

    private static void PopMatchingDelimiter(List<char> delimiters, char character)
    {
        if (delimiters.Count != 0 && delimiters[delimiters.Count - 1] == character)
        {
            delimiters.RemoveAt(delimiters.Count - 1);
        }
    }

    private readonly record struct AttributeValue(string Name, int Start, int End);

    private readonly record struct ClassRegion(
        int Start,
        int End,
        bool RejectsStartBoundary,
        bool RejectsEndBoundary);

    private readonly record struct TextSpan(int Start, int Length)
    {
        internal int End => Start + Length;
    }
}
