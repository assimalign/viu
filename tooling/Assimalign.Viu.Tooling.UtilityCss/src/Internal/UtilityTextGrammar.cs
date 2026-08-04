using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityTextGrammar
{
    public static bool TrySegment(
        string text,
        char separator,
        int sourceOffset,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken,
        out List<TextSegment> segments)
    {
        segments = new List<TextSegment>();
        var closingDelimiters = new List<char>();
        var segmentStart = 0;
        var quote = '\0';
        var isEscaped = false;

        for (var index = 0; index < text.Length; index++)
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

            if (IsClosingDelimiter(character))
            {
                if (closingDelimiters.Count == 0 ||
                    closingDelimiters[closingDelimiters.Count - 1] != character)
                {
                    AddUnbalancedDiagnostic(
                        diagnostics,
                        sourceOffset + index,
                        1,
                        $"Unexpected closing delimiter '{character}'.");
                    return false;
                }

                closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                continue;
            }

            if (character == separator && closingDelimiters.Count == 0)
            {
                segments.Add(
                    new TextSegment(
                        text.Substring(segmentStart, index - segmentStart),
                        sourceOffset + segmentStart));
                segmentStart = index + 1;
            }
        }

        if (isEscaped)
        {
            AddUnbalancedDiagnostic(
                diagnostics,
                sourceOffset + Math.Max(0, text.Length - 1),
                Math.Min(1, text.Length),
                "The final escape character does not escape a value.");
            return false;
        }

        if (quote != '\0')
        {
            AddUnbalancedDiagnostic(
                diagnostics,
                sourceOffset,
                text.Length,
                $"The '{quote}' string is not terminated.");
            return false;
        }

        if (closingDelimiters.Count != 0)
        {
            AddUnbalancedDiagnostic(
                diagnostics,
                sourceOffset,
                text.Length,
                $"The candidate is missing a '{closingDelimiters[closingDelimiters.Count - 1]}' delimiter.");
            return false;
        }

        segments.Add(
            new TextSegment(
                text.Substring(segmentStart),
                sourceOffset + segmentStart));
        return true;
    }

    public static bool IsValidArbitraryValue(
        string text,
        CancellationToken cancellationToken)
    {
        var closingDelimiters = new List<char>();
        var quote = '\0';
        var isEscaped = false;

        for (var index = 0; index < text.Length; index++)
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

            if (character == '{' && closingDelimiters.Count == 0)
            {
                return false;
            }

            if (character == ';' && closingDelimiters.Count == 0)
            {
                return false;
            }

            if (TryGetClosingDelimiter(character, out var closingDelimiter))
            {
                closingDelimiters.Add(closingDelimiter);
                continue;
            }

            if (IsClosingDelimiter(character))
            {
                if (closingDelimiters.Count == 0 ||
                    closingDelimiters[closingDelimiters.Count - 1] != character)
                {
                    return false;
                }

                closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
            }
        }

        return !isEscaped && quote == '\0' && closingDelimiters.Count == 0;
    }

    public static string DecodeArbitraryValue(
        string text,
        CancellationToken cancellationToken)
    {
        var result = new StringBuilder(text.Length);
        var functionContexts = new List<FunctionContext>();
        var quote = '\0';

        for (var index = 0; index < text.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (character == '\\' &&
                index + 1 < text.Length &&
                text[index + 1] == '_')
            {
                result.Append('_');
                index++;
                continue;
            }

            if (character == '\\' && index + 1 < text.Length)
            {
                result.Append(character);
                result.Append(text[index + 1]);
                index++;
                continue;
            }

            if (quote != '\0')
            {
                result.Append(
                    character == '_' && !ShouldPreserveUnderscore(functionContexts)
                        ? ' '
                        : character);

                if (character == quote)
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
                functionContexts.Add(
                    new FunctionContext(FindFunctionName(text, index)));
                result.Append(character);
                continue;
            }

            if (character == ')' && functionContexts.Count != 0)
            {
                functionContexts.RemoveAt(functionContexts.Count - 1);
                result.Append(character);
                continue;
            }

            if (character == ',' && functionContexts.Count != 0)
            {
                var contextIndex = functionContexts.Count - 1;
                var context = functionContexts[contextIndex];
                if (context.IsVariableFunction)
                {
                    functionContexts[contextIndex] = context.AfterFirstArgument();
                }

                result.Append(character);
                continue;
            }

            if (character == '_')
            {
                result.Append(
                    ShouldPreserveUnderscore(functionContexts)
                        ? '_'
                        : ' ');
                continue;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    public static bool IsValidNamedValue(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var character in text)
        {
            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character is '_' or '.' or '%' or '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static bool TryGetArbitraryTypeHint(
        string decodedValue,
        out string? dataType,
        out string value)
    {
        dataType = null;
        value = decodedValue;

        for (var index = 0; index < decodedValue.Length; index++)
        {
            var character = decodedValue[index];
            if (character == ':')
            {
                if (index == 0)
                {
                    return false;
                }

                dataType = decodedValue.Substring(0, index);
                value = decodedValue.Substring(index + 1);
                return true;
            }

            if (character == '-' ||
                (character >= 'a' && character <= 'z'))
            {
                continue;
            }

            return true;
        }

        return true;
    }

    private static bool TryGetClosingDelimiter(
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

    private static bool IsClosingDelimiter(char character) =>
        character is ')' or ']' or '}';

    private static void AddUnbalancedDiagnostic(
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        int start,
        int length,
        string message)
    {
        diagnostics.Add(
            new UtilityCandidateDiagnostic(
                UtilityCandidateDiagnosticCode.UnbalancedDelimiter,
                UtilityCandidateDiagnosticSeverity.Error,
                message,
                start,
                length));
    }

    private static string FindFunctionName(string text, int openParenthesisIndex)
    {
        var start = openParenthesisIndex - 1;
        while (start >= 0)
        {
            var character = text[start];
            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character is '-' or '_')
            {
                start--;
                continue;
            }

            break;
        }

        return text.Substring(start + 1, openParenthesisIndex - start - 1);
    }

    private static bool ShouldPreserveUnderscore(
        IReadOnlyList<FunctionContext> functionContexts)
    {
        for (var index = functionContexts.Count - 1; index >= 0; index--)
        {
            var context = functionContexts[index];
            if (context.IsUniformResourceLocatorFunction)
            {
                return true;
            }

            if (context.IsVariableFunction && context.IsFirstArgument)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct FunctionContext
    {
        public FunctionContext(string name)
            : this(
                name == "url" || name.EndsWith("_url", StringComparison.Ordinal),
                name == "var" ||
                name.EndsWith("_var", StringComparison.Ordinal) ||
                name == "theme" ||
                name.EndsWith("_theme", StringComparison.Ordinal),
                true)
        {
        }

        private FunctionContext(
            bool isUniformResourceLocatorFunction,
            bool isVariableFunction,
            bool isFirstArgument)
        {
            IsUniformResourceLocatorFunction = isUniformResourceLocatorFunction;
            IsVariableFunction = isVariableFunction;
            IsFirstArgument = isFirstArgument;
        }

        public bool IsUniformResourceLocatorFunction { get; }

        public bool IsVariableFunction { get; }

        public bool IsFirstArgument { get; }

        public FunctionContext AfterFirstArgument() =>
            new(IsUniformResourceLocatorFunction, IsVariableFunction, false);
    }
}
