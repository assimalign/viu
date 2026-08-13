using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityCssText
{
    public static string EscapeClassName(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '\0')
            {
                result.Append('\uFFFD');
                continue;
            }

            var isFirstDigit = index == 0 && IsBasicLatinDigit(character);
            var isSecondDigitAfterHyphen =
                index == 1 &&
                value[0] == '-' &&
                IsBasicLatinDigit(character);
            if (isFirstDigit || isSecondDigitAfterHyphen)
            {
                result.Append('\\');
                result.Append(
                    ((int)character).ToString(
                        "x",
                        CultureInfo.InvariantCulture));
                result.Append(' ');
                continue;
            }

            if (IsBasicLatinLetter(character) ||
                IsBasicLatinDigit(character) ||
                character is '-' or '_' ||
                character >= '\u0080')
            {
                result.Append(character);
                continue;
            }

            result.Append('\\');
            result.Append(character);
        }

        return result.ToString();
    }

    public static bool IsSafeProperty(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var start = value.StartsWith("--", StringComparison.Ordinal)
            ? 2
            : 0;
        if (start == value.Length)
        {
            return false;
        }

        if (start == 0 &&
            !IsBasicLatinLetter(value[0]) &&
            value[0] != '-')
        {
            return false;
        }

        for (var index = start; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsBasicLatinLetter(character) &&
                !IsBasicLatinDigit(character) &&
                character != '-')
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsSafeValue(string value)
    {
        var delimiters = new Stack<char>();
        char quote = '\0';
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
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

            if (character == '{' || character == '}')
            {
                return false;
            }

            if (character is '(' or '[')
            {
                delimiters.Push(character);
                continue;
            }

            if (character is ')' or ']')
            {
                if (delimiters.Count == 0)
                {
                    return false;
                }

                var opening = delimiters.Pop();
                if ((opening == '(' && character != ')') ||
                    (opening == '[' && character != ']'))
                {
                    return false;
                }

                continue;
            }

            if (character == ';' && delimiters.Count == 0)
            {
                return false;
            }
        }

        return !escaped && quote == '\0' && delimiters.Count == 0;
    }

    public static string AddWhitespaceAroundMathOperators(string value)
    {
        if (value.IndexOf("calc(", StringComparison.OrdinalIgnoreCase) < 0 &&
            value.IndexOf("clamp(", StringComparison.OrdinalIgnoreCase) < 0 &&
            value.IndexOf("max(", StringComparison.OrdinalIgnoreCase) < 0 &&
            value.IndexOf("min(", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return value;
        }

        var result = new StringBuilder(value.Length + 8);
        var mathContexts = new Stack<bool>();
        char quote = '\0';
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (quote != '\0')
            {
                result.Append(character);
                if (character == quote &&
                    (index == 0 || value[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                result.Append(character);
                continue;
            }

            if (character == '(')
            {
                var functionEnd = index;
                var functionStart = functionEnd;
                while (functionStart > 0 &&
                       IsBasicLatinLetter(value[functionStart - 1]))
                {
                    functionStart--;
                }

                var functionName = value.Substring(
                    functionStart,
                    functionEnd - functionStart);
                var isMathContext =
                    (mathContexts.Count != 0 && mathContexts.Peek()) ||
                    functionName.Equals("calc", StringComparison.OrdinalIgnoreCase) ||
                    functionName.Equals("clamp", StringComparison.OrdinalIgnoreCase) ||
                    functionName.Equals("max", StringComparison.OrdinalIgnoreCase) ||
                    functionName.Equals("min", StringComparison.OrdinalIgnoreCase);
                mathContexts.Push(isMathContext);
                result.Append(character);
                continue;
            }

            if (character == ')')
            {
                if (mathContexts.Count != 0)
                {
                    mathContexts.Pop();
                }

                result.Append(character);
                continue;
            }

            if (mathContexts.Count != 0 &&
                mathContexts.Peek() &&
                IsBinaryMathOperator(value, index))
            {
                if (result.Length != 0 &&
                    !char.IsWhiteSpace(result[result.Length - 1]))
                {
                    result.Append(' ');
                }

                result.Append(character);
                if (index + 1 < value.Length &&
                    !char.IsWhiteSpace(value[index + 1]))
                {
                    result.Append(' ');
                }

                continue;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    public static string RenderRule(
        string selector,
        IReadOnlyList<UtilityCssDeclaration> declarations,
        IReadOnlyList<string> wrappers,
        bool isImportant)
    {
        var result = new StringBuilder();
        var depth = 0;
        foreach (var wrapper in wrappers)
        {
            AppendIndent(result, depth);
            result.Append(wrapper);
            result.AppendLine(" {");
            depth++;
        }

        AppendIndent(result, depth);
        result.Append(selector);
        result.AppendLine(" {");
        foreach (var declaration in declarations)
        {
            AppendIndent(result, depth + 1);
            result.Append(declaration.Property);
            result.Append(": ");
            result.Append(declaration.Value);
            if (isImportant)
            {
                result.Append(" !important");
            }

            result.AppendLine(";");
        }

        AppendIndent(result, depth);
        result.Append('}');
        while (depth > 0)
        {
            result.AppendLine();
            depth--;
            AppendIndent(result, depth);
            result.Append('}');
        }

        return result.ToString();
    }

    private static void AppendIndent(
        StringBuilder builder,
        int depth)
    {
        builder.Append(' ', depth * 2);
    }

    private static bool IsBasicLatinDigit(char character) =>
        character >= '0' && character <= '9';

    private static bool IsBasicLatinLetter(char character) =>
        (character >= 'a' && character <= 'z') ||
        (character >= 'A' && character <= 'Z');

    private static bool IsBinaryMathOperator(
        string value,
        int index)
    {
        var character = value[index];
        if (character is not ('+' or '-' or '*' or '/'))
        {
            return false;
        }

        var previousIndex = index - 1;
        while (previousIndex >= 0 &&
               char.IsWhiteSpace(value[previousIndex]))
        {
            previousIndex--;
        }

        var nextIndex = index + 1;
        while (nextIndex < value.Length &&
               char.IsWhiteSpace(value[nextIndex]))
        {
            nextIndex++;
        }

        if (previousIndex < 0 ||
            nextIndex >= value.Length)
        {
            return false;
        }

        var previous = value[previousIndex];
        var next = value[nextIndex];
        if (previous is '(' or ',' or '+' or '-' or '*' or '/' ||
            next is ')' or ',' or '+' or '*' or '/')
        {
            return false;
        }

        return true;
    }
}
