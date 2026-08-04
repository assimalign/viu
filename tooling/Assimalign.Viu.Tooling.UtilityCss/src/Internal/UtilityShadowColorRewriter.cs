using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityShadowColorRewriter
{
    private static readonly HashSet<string> ColorFunctions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "--alpha",
            "color",
            "color-mix",
            "contrast-color",
            "device-cmyk",
            "hsl",
            "hsla",
            "hwb",
            "lab",
            "lch",
            "light-dark",
            "oklab",
            "oklch",
            "rgb",
            "rgba",
        };

    private static readonly HashSet<string> Keywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "inherit",
            "initial",
            "inset",
            "revert",
            "unset",
        };

    private static readonly HashSet<string> LengthFunctions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "--spacing",
            "calc",
            "clamp",
            "max",
            "min",
        };

    public static string EnsureInset(string value)
    {
        var result = new List<string>();
        foreach (var segment in SplitAtTopLevel(value, ','))
        {
            var shadow = segment.Trim();
            if (shadow.Length == 0 ||
                shadow.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                shadow.StartsWith("inset ", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(shadow);
            }
            else
            {
                result.Add("inset " + shadow);
            }
        }

        return string.Join(", ", result);
    }

    public static string ToDropShadowFunctions(
        string value,
        string colorProperty)
    {
        var result = new List<string>();
        foreach (var segment in SplitAtTopLevel(value, ','))
        {
            var shadow = ReplaceColors(
                segment.Trim(),
                colorProperty);
            result.Add("drop-shadow(" + shadow + ")");
        }

        return string.Join(" ", result);
    }

    public static string ReplaceColors(
        string value,
        string colorProperty)
    {
        var result = new List<string>();
        foreach (var segment in SplitAtTopLevel(value, ','))
        {
            result.Add(
                ReplaceSegmentColor(
                    segment.Trim(),
                    colorProperty));
        }

        return string.Join(", ", result);
    }

    private static string ReplaceSegmentColor(
        string shadow,
        string colorProperty)
    {
        var tokens = Tokenize(shadow);
        var lengthCount = 0;
        var unknownIndexes = new List<int>();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (Keywords.Contains(token))
            {
                continue;
            }

            if (IsLength(token))
            {
                lengthCount++;
                continue;
            }

            if (IsColor(token))
            {
                tokens[index] = Wrap(
                    colorProperty,
                    token);
                return string.Join(" ", tokens);
            }

            if (token.StartsWith(
                    "var(" + colorProperty + ",",
                    StringComparison.Ordinal))
            {
                return string.Join(" ", tokens);
            }

            unknownIndexes.Add(index);
        }

        if (lengthCount < 2)
        {
            return shadow;
        }

        if (unknownIndexes.Count == 0)
        {
            tokens.Add(
                Wrap(
                    colorProperty,
                    "currentcolor"));
            return string.Join(" ", tokens);
        }

        if (unknownIndexes.Count == 1)
        {
            var unknownIndex = unknownIndexes[0];
            tokens[unknownIndex] = Wrap(
                colorProperty,
                tokens[unknownIndex]);
            return string.Join(" ", tokens);
        }

        return shadow;
    }

    private static string Wrap(
        string colorProperty,
        string color) =>
        "var(" + colorProperty + ", " + color + ")";

    private static bool IsColor(string token)
    {
        if (token.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        var parenthesis = token.IndexOf('(');
        return parenthesis > 0 &&
            ColorFunctions.Contains(token.Substring(0, parenthesis));
    }

    private static bool IsLength(string token)
    {
        var parenthesis = token.IndexOf('(');
        if (parenthesis > 0)
        {
            return LengthFunctions.Contains(
                token.Substring(0, parenthesis));
        }

        var index = 0;
        if (token.Length > 0 &&
            token[0] is '+' or '-')
        {
            index++;
        }

        var hasDigit = false;
        var hasDecimalPoint = false;
        while (index < token.Length)
        {
            var character = token[index];
            if (char.IsDigit(character))
            {
                hasDigit = true;
                index++;
                continue;
            }

            if (character == '.' &&
                !hasDecimalPoint)
            {
                hasDecimalPoint = true;
                index++;
                continue;
            }

            break;
        }

        return hasDigit;
    }

    private static List<string> Tokenize(string value)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')' &&
                     depth > 0)
            {
                depth--;
            }

            if (char.IsWhiteSpace(character) &&
                depth == 0)
            {
                if (token.Length > 0)
                {
                    result.Add(token.ToString());
                    token.Clear();
                }

                continue;
            }

            token.Append(character);
        }

        if (token.Length > 0)
        {
            result.Add(token.ToString());
        }

        return result;
    }

    private static List<string> SplitAtTopLevel(
        string value,
        char separator)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')' &&
                     depth > 0)
            {
                depth--;
            }
            else if (character == separator &&
                     depth == 0)
            {
                result.Add(
                    value.Substring(
                        start,
                        index - start));
                start = index + 1;
            }
        }

        result.Add(value.Substring(start));
        return result;
    }
}
