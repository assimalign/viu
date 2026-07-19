using System;
using System.Globalization;

namespace Assimalign.Viu.Shared;

/// <summary>
/// JavaScript-style numeric coercion — the C# port of <c>looseToNumber</c> and <c>toNumber</c>
/// from <c>@vue/shared</c> (<c>packages/shared/src/general.ts</c>). The <c>.number</c> modifier
/// on <c>v-model</c> (https://vuejs.org/guide/essentials/forms.html#lazy) runs the raw DOM string
/// through <see cref="LooseToNumber(object?)"/>: a value whose leading portion parses as a number
/// becomes that number, and anything else is left untouched — so <c>"12abc"</c> yields <c>12</c>
/// but <c>"abc"</c> and <c>""</c> stay strings. Parsing is invariant-culture (mandatory so SSR and
/// client hydration agree) with no reflection.
/// </summary>
public static class NumberCoercion
{
    /// <summary>
    /// The C# port of upstream's <c>looseToNumber</c> — <c>parseFloat</c> semantics: the longest
    /// leading numeric prefix (after leading whitespace, an optional sign, digits, an optional
    /// fraction and exponent, or <c>Infinity</c>) becomes a <see cref="double"/>; a value with no
    /// numeric prefix is returned unchanged (upstream: <c>const n = parseFloat(val); return
    /// isNaN(n) ? val : n</c>).
    /// </summary>
    /// <param name="value">The raw value, typically a DOM element's string value.</param>
    /// <returns>The parsed <see cref="double"/>, or <paramref name="value"/> unchanged when it has no numeric prefix.</returns>
    public static object? LooseToNumber(object? value)
    {
        if (value is not string text)
        {
            // v-model always hands this a string; other shapes pass through (upstream coerces via
            // String(val) first, but the runtime only feeds it DOM string values).
            return value;
        }
        return TryParseFloatPrefix(text, out var number) ? number : value;
    }

    /// <summary>
    /// The C# port of upstream's <c>toNumber</c> — stricter than <see cref="LooseToNumber(object?)"/>:
    /// the <em>entire</em> string must parse as a number (upstream: <c>looseToNumber</c> uses
    /// <c>parseFloat(val)</c>, while <c>toNumber</c> uses <c>Number(val)</c>), otherwise the value
    /// is returned unchanged. Used where a whole-string numeric value is expected (e.g. compiled
    /// numeric props), not the partial-prefix <c>v-model.number</c> path.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The parsed <see cref="double"/>, or <paramref name="value"/> unchanged when it is not wholly numeric.</returns>
    public static object? ToNumber(object? value)
    {
        if (value is not string text)
        {
            return value;
        }
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            // Number("") === 0 in JavaScript; toNumber therefore returns 0.
            return 0d;
        }
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : value;
    }

    // parseFloat: skip leading whitespace, then match the longest StrDecimalLiteral prefix
    // (optional sign, Infinity, or digits/fraction/exponent). Returns false when no numeric
    // prefix exists (JavaScript's NaN result), matching ECMA-262 parseFloat.
    private static bool TryParseFloatPrefix(string text, out double number)
    {
        number = 0d;
        var span = text.AsSpan();
        var index = 0;
        while (index < span.Length && char.IsWhiteSpace(span[index]))
        {
            index++;
        }
        var start = index;
        var negative = false;
        if (index < span.Length && (span[index] == '+' || span[index] == '-'))
        {
            negative = span[index] == '-';
            index++;
        }
        // parseFloat('Infinity') / parseFloat('-Infinity').
        if (span[index..].StartsWith("Infinity".AsSpan(), StringComparison.Ordinal))
        {
            number = negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }
        var integerDigits = 0;
        while (index < span.Length && char.IsAsciiDigit(span[index]))
        {
            index++;
            integerDigits++;
        }
        var fractionDigits = 0;
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
        // Optional exponent; roll back when 'e' is not followed by (sign?) digits.
        if (index < span.Length && (span[index] == 'e' || span[index] == 'E'))
        {
            var afterExponent = index + 1;
            if (afterExponent < span.Length && (span[afterExponent] == '+' || span[afterExponent] == '-'))
            {
                afterExponent++;
            }
            var exponentDigits = 0;
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
        var numeric = span[start..index];
        // A lone trailing '.' (e.g. "5.") is valid in JavaScript but not every invariant parse;
        // drop it so the numeric portion round-trips.
        if (numeric[^1] == '.')
        {
            numeric = numeric[..^1];
        }
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
