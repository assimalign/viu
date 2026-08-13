using System;
using System.Globalization;

namespace Assimalign.Viu.Browser;

/// <summary>Coerces DOM form strings with invariant <c>parseFloat</c>-style semantics.</summary>
/// <remarks>
/// A leading numeric portion becomes a <see cref="double"/> while a value with no numeric prefix
/// remains unchanged. This prevents partially typed non-numeric input from being destroyed.
/// Specified by <c>[SFC-CG-7]</c>.
/// </remarks>
internal static class NumberCoercion
{
    public static object? LooseToNumber(object? value)
    {
        if (value is not string text)
        {
            return value;
        }

        return TryParseFloatPrefix(text, out double number) ? number : value;
    }

    /// <summary>
    /// Coerces a wholly numeric string, returning the original value when any non-numeric suffix
    /// remains. An empty string becomes zero. This is the strict counterpart to the prefix-based
    /// <see cref="LooseToNumber(object?)"/> form-binding path.
    /// </summary>
    public static object? ToNumber(object? value)
    {
        if (value is not string text)
        {
            return value;
        }

        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return 0d;
        }

        return double.TryParse(
            trimmed,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double number)
                ? number
                : value;
    }

    private static bool TryParseFloatPrefix(string text, out double number)
    {
        number = 0;
        ReadOnlySpan<char> span = text.AsSpan();
        int index = 0;
        while (index < span.Length && char.IsWhiteSpace(span[index]))
        {
            index++;
        }

        int start = index;
        bool negative = false;
        if (index < span.Length && (span[index] == '+' || span[index] == '-'))
        {
            negative = span[index] == '-';
            index++;
        }

        if (span[index..].StartsWith("Infinity".AsSpan(), StringComparison.Ordinal))
        {
            number = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        int integerDigits = 0;
        while (index < span.Length && char.IsAsciiDigit(span[index]))
        {
            index++;
            integerDigits++;
        }

        int fractionDigits = 0;
        if (index < span.Length && span[index] == '.')
        {
            index++;
            while (index < span.Length && char.IsAsciiDigit(span[index]))
            {
                index++;
                fractionDigits++;
            }
        }

        if (integerDigits == 0 && fractionDigits == 0)
        {
            return false;
        }

        if (index < span.Length && (span[index] == 'e' || span[index] == 'E'))
        {
            int afterExponent = index + 1;
            if (afterExponent < span.Length
                && (span[afterExponent] == '+' || span[afterExponent] == '-'))
            {
                afterExponent++;
            }

            int exponentDigits = 0;
            while (afterExponent < span.Length && char.IsAsciiDigit(span[afterExponent]))
            {
                afterExponent++;
                exponentDigits++;
            }

            if (exponentDigits > 0)
            {
                index = afterExponent;
            }
        }

        ReadOnlySpan<char> numeric = span[start..index];
        if (numeric[^1] == '.')
        {
            numeric = numeric[..^1];
        }

        return double.TryParse(
            numeric,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);
    }
}
