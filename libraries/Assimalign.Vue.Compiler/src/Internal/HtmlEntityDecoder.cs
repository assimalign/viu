using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// Decodes HTML character references (named and numeric) in template text, attribute values, and
/// interpolation expressions. The C# port of the decoding behaviour Vue 3.5 gets from the
/// <c>entities</c> package (<c>decodeHTML</c> / <c>DecodingMode</c>): longest-match over the embedded
/// <see cref="HtmlNamedCharacterReferences"/> table, the legacy no-semicolon rule, and the WHATWG
/// numeric code-point sanitisation. Purely table-driven — no runtime DOM or network access.
/// </summary>
/// <remarks>
/// Whereas Vue's tokenizer decodes incrementally and splits text around each reference, this port
/// decodes a whole raw slice when the parser materialises a node's content. The observable AST is the
/// same: the node's <c>Content</c> is decoded while its <c>Location.Source</c> stays the raw slice.
/// Numeric reference rules: https://html.spec.whatwg.org/multipage/parsing.html#numeric-character-reference-end-state.
/// </remarks>
internal static class HtmlEntityDecoder
{
    // Windows-1252 replacements for C1 control code points, per the WHATWG numeric character reference
    // rules (mirrors the `entities` package's decode_codepoint map).
    private static readonly Dictionary<int, int> C1Replacements = new()
    {
        [0x80] = 0x20AC, [0x82] = 0x201A, [0x83] = 0x0192, [0x84] = 0x201E,
        [0x85] = 0x2026, [0x86] = 0x2020, [0x87] = 0x2021, [0x88] = 0x02C6,
        [0x89] = 0x2030, [0x8A] = 0x0160, [0x8B] = 0x2039, [0x8C] = 0x0152,
        [0x8E] = 0x017D, [0x91] = 0x2018, [0x92] = 0x2019, [0x93] = 0x201C,
        [0x94] = 0x201D, [0x95] = 0x2022, [0x96] = 0x2013, [0x97] = 0x2014,
        [0x98] = 0x02DC, [0x99] = 0x2122, [0x9A] = 0x0161, [0x9B] = 0x203A,
        [0x9C] = 0x0153, [0x9E] = 0x017E, [0x9F] = 0x0178,
    };

    /// <summary>
    /// Decodes every valid character reference in <paramref name="input"/>. Text of a matched reference
    /// is replaced by its decoded characters; an ampersand that begins no valid reference is left as-is.
    /// </summary>
    /// <param name="input">The raw slice to decode.</param>
    /// <param name="inAttribute">
    /// <see langword="true"/> in an attribute value, where a named reference without a trailing
    /// <c>;</c> is not decoded when followed by <c>=</c> or an alphanumeric character (the legacy
    /// ambiguous-ampersand rule).
    /// </param>
    public static string Decode(string input, bool inAttribute)
    {
        if (input.IndexOf('&') < 0)
        {
            return input;
        }

        var builder = new StringBuilder(input.Length);
        var index = 0;
        while (index < input.Length)
        {
            var current = input[index];
            if (current == '&' && TryDecodeReference(input, index, inAttribute, out var decoded, out var consumed))
            {
                builder.Append(decoded);
                index += consumed;
            }
            else
            {
                builder.Append(current);
                index++;
            }
        }

        return builder.ToString();
    }

    private static bool TryDecodeReference(string input, int ampersandIndex, bool inAttribute, out string decoded, out int consumed)
    {
        decoded = string.Empty;
        consumed = 0;
        var start = ampersandIndex + 1;
        if (start >= input.Length)
        {
            return false;
        }

        return input[start] == '#'
            ? TryDecodeNumeric(input, ampersandIndex, out decoded, out consumed)
            : TryDecodeNamed(input, ampersandIndex, inAttribute, out decoded, out consumed);
    }

    private static bool TryDecodeNumeric(string input, int ampersandIndex, out string decoded, out int consumed)
    {
        decoded = string.Empty;
        consumed = 0;
        var index = ampersandIndex + 2; // skip "&#"
        var hex = false;
        if (index < input.Length && (input[index] == 'x' || input[index] == 'X'))
        {
            hex = true;
            index++;
        }

        var digitsStart = index;
        long code = 0;
        while (index < input.Length)
        {
            var value = DigitValue(input[index], hex);
            if (value < 0)
            {
                break;
            }

            if (code <= 0x10FFFF)
            {
                code = (code * (hex ? 16 : 10)) + value;
            }

            index++;
        }

        if (index == digitsStart)
        {
            // No digits: not a valid numeric reference (e.g. "&#;" or "&#x").
            return false;
        }

        if (index < input.Length && input[index] == ';')
        {
            index++;
        }

        decoded = FromCodePoint(SanitizeCodePoint(code));
        consumed = index - ampersandIndex;
        return true;
    }

    private static bool TryDecodeNamed(string input, int ampersandIndex, bool inAttribute, out string decoded, out int consumed)
    {
        decoded = string.Empty;
        consumed = 0;
        var nameStart = ampersandIndex + 1;
        var index = nameStart;
        while (index < input.Length && IsAlphanumeric(input[index]))
        {
            index++;
        }

        if (index == nameStart)
        {
            return false;
        }

        // The maximal candidate spans the alphanumeric run plus an optional trailing ';'.
        var maximalEnd = index;
        if (index < input.Length && input[index] == ';')
        {
            maximalEnd = index + 1;
        }

        // Longest match wins: probe from the maximal candidate down to a single character.
        for (var end = maximalEnd; end > nameStart; end--)
        {
            var key = input.Substring(nameStart, end - nameStart);
            if (!HtmlNamedCharacterReferences.Table.TryGetValue(key, out var value))
            {
                continue;
            }

            var terminatedBySemicolon = key[key.Length - 1] == ';';
            if (inAttribute && !terminatedBySemicolon && end < input.Length)
            {
                // Legacy ambiguous-ampersand rule: leave the reference undecoded in an attribute value
                // when it lacks a ';' and is followed by '=' or an alphanumeric character.
                var following = input[end];
                if (following == '=' || IsAlphanumeric(following))
                {
                    return false;
                }
            }

            decoded = value;
            consumed = end - ampersandIndex;
            return true;
        }

        return false;
    }

    private static int SanitizeCodePoint(long code)
    {
        if (code == 0 || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF))
        {
            return 0xFFFD;
        }

        return C1Replacements.TryGetValue((int)code, out var replacement) ? replacement : (int)code;
    }

    private static string FromCodePoint(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static int DigitValue(char character, bool hex)
    {
        if (character >= '0' && character <= '9')
        {
            return character - '0';
        }

        if (hex)
        {
            if (character >= 'a' && character <= 'f')
            {
                return character - 'a' + 10;
            }

            if (character >= 'A' && character <= 'F')
            {
                return character - 'A' + 10;
            }
        }

        return -1;
    }

    private static bool IsAlphanumeric(char character)
        => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z');
}
