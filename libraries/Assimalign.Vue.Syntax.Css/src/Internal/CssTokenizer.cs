using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The CSS Syntax Module Level 3 tokenizer (https://www.w3.org/TR/css-syntax-3/#tokenization): scans a
/// source string into a <see cref="CssToken"/> stream, index-based and allocation-light (tokens carry
/// offsets, not substrings). Tokenizing is recoverable per the spec's error handling — an unterminated
/// string or comment reports a diagnostic and is consumed to a safe stopping point rather than throwing.
/// </summary>
/// <remarks>
/// The token set is trimmed to what rule-level parsing and scoped-selector rewriting need (see
/// <see cref="CssTokenKind"/>); <c>url()</c> and unicode-range are not special-cased, which never affects
/// selector scoping or block structure.
/// </remarks>
internal sealed class CssTokenizer
{
    private readonly string source;
    private readonly CssPositionMap positions;
    private readonly List<CssError> diagnostics;
    private int index;

    /// <summary>Creates a tokenizer over <paramref name="source"/>.</summary>
    /// <param name="source">The CSS source string.</param>
    /// <param name="positions">The shared position map used to locate lexical diagnostics.</param>
    /// <param name="diagnostics">The sink recoverable lexical diagnostics are appended to.</param>
    public CssTokenizer(string source, CssPositionMap positions, List<CssError> diagnostics)
    {
        this.source = source;
        this.positions = positions;
        this.diagnostics = diagnostics;
    }

    /// <summary>Tokenizes the whole source, ending with a single <see cref="CssTokenKind.EndOfFile"/> token.</summary>
    /// <returns>The token stream in source order.</returns>
    public List<CssToken> Tokenize()
    {
        var tokens = new List<CssToken>();
        while (index < source.Length)
        {
            tokens.Add(NextToken());
        }

        tokens.Add(new CssToken(CssTokenKind.EndOfFile, source.Length, source.Length));
        return tokens;
    }

    private CssToken NextToken()
    {
        var start = index;
        var character = source[index];

        if (IsWhitespace(character))
        {
            return ConsumeWhitespace(start);
        }

        switch (character)
        {
            case '/' when Peek(1) == '*':
                return ConsumeComment(start);
            case '"':
            case '\'':
                return ConsumeString(start, character);
            case '#':
                if (IsNameChar(Peek(1)) || IsValidEscape(Peek(1), Peek(2)))
                {
                    index++;
                    ConsumeName();
                    return new CssToken(CssTokenKind.Hash, start, index);
                }

                index++;
                return new CssToken(CssTokenKind.Delim, start, index);
            case '@':
                if (IsNameStart(Peek(1)) || (Peek(1) == '-') || IsValidEscape(Peek(1), Peek(2)))
                {
                    index++;
                    ConsumeName();
                    return new CssToken(CssTokenKind.AtKeyword, start, index);
                }

                index++;
                return new CssToken(CssTokenKind.Delim, start, index);
            case '(':
                index++;
                return new CssToken(CssTokenKind.LeftParenthesis, start, index);
            case ')':
                index++;
                return new CssToken(CssTokenKind.RightParenthesis, start, index);
            case '[':
                index++;
                return new CssToken(CssTokenKind.LeftBracket, start, index);
            case ']':
                index++;
                return new CssToken(CssTokenKind.RightBracket, start, index);
            case '{':
                index++;
                return new CssToken(CssTokenKind.LeftBrace, start, index);
            case '}':
                index++;
                return new CssToken(CssTokenKind.RightBrace, start, index);
            case ':':
                index++;
                return new CssToken(CssTokenKind.Colon, start, index);
            case ';':
                index++;
                return new CssToken(CssTokenKind.Semicolon, start, index);
            case ',':
                index++;
                return new CssToken(CssTokenKind.Comma, start, index);
        }

        if (IsDigit(character) || ((character == '+' || character == '-' || character == '.') && StartsNumber()))
        {
            return ConsumeNumber(start);
        }

        if (IsNameStart(character) || IsValidEscape(character, Peek(1)))
        {
            ConsumeName();
            if (index < source.Length && source[index] == '(')
            {
                index++;
                return new CssToken(CssTokenKind.Function, start, index);
            }

            return new CssToken(CssTokenKind.Ident, start, index);
        }

        index++;
        return new CssToken(CssTokenKind.Delim, start, index);
    }

    private CssToken ConsumeWhitespace(int start)
    {
        while (index < source.Length && IsWhitespace(source[index]))
        {
            index++;
        }

        return new CssToken(CssTokenKind.Whitespace, start, index);
    }

    private CssToken ConsumeComment(int start)
    {
        index += 2; // consume the opening "/*"
        while (index < source.Length)
        {
            if (source[index] == '*' && Peek(1) == '/')
            {
                index += 2;
                return new CssToken(CssTokenKind.Comment, start, index);
            }

            index++;
        }

        diagnostics.Add(new CssError(CssErrorCode.UnterminatedComment, positions.LocationOf(start, source.Length)));
        return new CssToken(CssTokenKind.Comment, start, index);
    }

    private CssToken ConsumeString(int start, char quote)
    {
        index++; // consume the opening quote
        while (index < source.Length)
        {
            var character = source[index];
            if (character == quote)
            {
                index++;
                return new CssToken(CssTokenKind.String, start, index);
            }

            if (character == '\n')
            {
                // An unescaped newline ends a bad string (CSS Syntax 3 §4.3.5); recover at the newline.
                diagnostics.Add(new CssError(CssErrorCode.UnterminatedString, positions.LocationOf(start, index)));
                return new CssToken(CssTokenKind.String, start, index);
            }

            if (character == '\\' && index + 1 < source.Length && source[index + 1] != '\n')
            {
                index += 2; // an escape consumes the backslash and the escaped code point
                continue;
            }

            index++;
        }

        diagnostics.Add(new CssError(CssErrorCode.UnterminatedString, positions.LocationOf(start, source.Length)));
        return new CssToken(CssTokenKind.String, start, index);
    }

    private CssToken ConsumeNumber(int start)
    {
        if (index < source.Length && (source[index] == '+' || source[index] == '-'))
        {
            index++;
        }

        while (index < source.Length && IsDigit(source[index]))
        {
            index++;
        }

        if (index < source.Length && source[index] == '.' && IsDigit(Peek(1)))
        {
            index++;
            while (index < source.Length && IsDigit(source[index]))
            {
                index++;
            }
        }

        if (index < source.Length && (source[index] == 'e' || source[index] == 'E'))
        {
            var exponent = index + 1;
            if (exponent < source.Length && (source[exponent] == '+' || source[exponent] == '-'))
            {
                exponent++;
            }

            if (exponent < source.Length && IsDigit(source[exponent]))
            {
                index = exponent + 1;
                while (index < source.Length && IsDigit(source[index]))
                {
                    index++;
                }
            }
        }

        // A trailing unit (dimension) or percent rides along; the numeric value stays one token because
        // the parser keeps raw value slices rather than typed numerics.
        if (index < source.Length && source[index] == '%')
        {
            index++;
        }
        else if (index < source.Length && (IsNameStart(source[index]) || IsValidEscape(source[index], Peek(1))))
        {
            ConsumeName();
        }

        return new CssToken(CssTokenKind.Number, start, index);
    }

    // Consumes a run of name code points (with escapes), leaving index just past the name.
    private void ConsumeName()
    {
        while (index < source.Length)
        {
            var character = source[index];
            if (IsNameChar(character))
            {
                index++;
            }
            else if (IsValidEscape(character, Peek(1)))
            {
                index += 2;
            }
            else
            {
                break;
            }
        }
    }

    private bool StartsNumber()
    {
        var first = source[index];
        if (IsDigit(first))
        {
            return true;
        }

        if (first == '+' || first == '-')
        {
            return IsDigit(Peek(1)) || (Peek(1) == '.' && IsDigit(Peek(2)));
        }

        if (first == '.')
        {
            return IsDigit(Peek(1));
        }

        return false;
    }

    private char Peek(int ahead)
    {
        var target = index + ahead;
        return target < source.Length ? source[target] : '\0';
    }

    private bool IsValidEscape(char first, char second) => first == '\\' && second != '\n';

    private static bool IsWhitespace(char character)
        => character == ' ' || character == '\t' || character == '\n' || character == '\r' || character == '\f';

    private static bool IsDigit(char character) => character >= '0' && character <= '9';

    private static bool IsNameStart(char character)
        => (character >= 'a' && character <= 'z')
            || (character >= 'A' && character <= 'Z')
            || character == '_'
            || character >= 0x80;

    private static bool IsNameChar(char character)
        => IsNameStart(character) || IsDigit(character) || character == '-';
}
