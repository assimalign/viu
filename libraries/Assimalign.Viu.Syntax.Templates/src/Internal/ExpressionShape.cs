using System.Text;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Lax structural checks over opaque expression text — is it a member expression, is it a function
/// expression — ported from the browser-build lexers in <c>@vue/compiler-core</c> <c>utils.ts</c>
/// (<c>isMemberExpressionBrowser</c>, <c>isFnExpressionBrowser</c>). This build never parses expression
/// bodies into a JavaScript AST ([V01.01.05.04] adds that), so — exactly like upstream's <c>__BROWSER__</c>
/// path — these string lexers decide the shape. False positives are invalid expressions anyway.
/// </summary>
internal static class ExpressionShape
{
    // Upstream fnExpRE.
    private static readonly Regex FunctionExpressionRegex = new(
        @"^\s*(async\s*)?(\([^)]*?\)|[\w$_]+)\s*(:[^=]+)?=>|^\s*(async\s+)?function(?:\s+[\w$]+)?\s*\(",
        RegexOptions.Compiled);

    // Upstream whitespaceRE: whitespace around . or [ is trimmed before member-expression lexing.
    private static readonly Regex WhitespaceAroundAccessRegex = new(@"\s+[.[]\s*|\s*[.[]\s+", RegexOptions.Compiled);

    private enum MemberLexState
    {
        InMemberExpression,
        InBrackets,
        InParentheses,
        InString,
    }

    private static string GetSource(ExpressionNode expression)
        => expression is SimpleExpressionNode simple ? simple.Content : expression.Location.Source;

    /// <summary>Whether <paramref name="expression"/> is a function expression (upstream <c>isFnExpression</c>).</summary>
    /// <param name="expression">The expression to test.</param>
    public static bool IsFunctionExpression(ExpressionNode expression)
        => FunctionExpressionRegex.IsMatch(GetSource(expression));

    /// <summary>
    /// Whether <paramref name="expression"/> is a member expression or plain identifier (upstream
    /// <c>isMemberExpressionBrowser</c>). Lax: only validates root-level structure, not bracket contents.
    /// </summary>
    /// <param name="expression">The expression to test.</param>
    public static bool IsMemberExpression(ExpressionNode expression)
    {
        var path = WhitespaceAroundAccessRegex.Replace(
            GetSource(expression).Trim(),
            match => match.Value.Trim());

        var state = MemberLexState.InMemberExpression;
        var stateStack = new System.Collections.Generic.Stack<MemberLexState>();
        var openBracketCount = 0;
        var openParenthesesCount = 0;
        var currentStringType = '\0';

        for (var index = 0; index < path.Length; index++)
        {
            var character = path[index];
            switch (state)
            {
                case MemberLexState.InMemberExpression:
                    if (character == '[')
                    {
                        stateStack.Push(state);
                        state = MemberLexState.InBrackets;
                        openBracketCount++;
                    }
                    else if (character == '(')
                    {
                        stateStack.Push(state);
                        state = MemberLexState.InParentheses;
                        openParenthesesCount++;
                    }
                    else if (!(index == 0 ? IsValidFirstIdentifierChar(character) : IsValidIdentifierChar(character)))
                    {
                        return false;
                    }

                    break;
                case MemberLexState.InBrackets:
                    if (character is '\'' or '"' or '`')
                    {
                        stateStack.Push(state);
                        state = MemberLexState.InString;
                        currentStringType = character;
                    }
                    else if (character == '[')
                    {
                        openBracketCount++;
                    }
                    else if (character == ']')
                    {
                        if (--openBracketCount == 0)
                        {
                            state = stateStack.Pop();
                        }
                    }

                    break;
                case MemberLexState.InParentheses:
                    if (character is '\'' or '"' or '`')
                    {
                        stateStack.Push(state);
                        state = MemberLexState.InString;
                        currentStringType = character;
                    }
                    else if (character == '(')
                    {
                        openParenthesesCount++;
                    }
                    else if (character == ')')
                    {
                        // an expression ending as a call is not a valid member expression
                        if (index == path.Length - 1)
                        {
                            return false;
                        }

                        if (--openParenthesesCount == 0)
                        {
                            state = stateStack.Pop();
                        }
                    }

                    break;
                case MemberLexState.InString:
                    if (character == currentStringType)
                    {
                        state = stateStack.Pop();
                        currentStringType = '\0';
                    }

                    break;
            }
        }

        return openBracketCount == 0 && openParenthesesCount == 0;
    }

    // validFirstIdentCharRE = /[A-Za-z_$\xA0-￿]/
    private static bool IsValidFirstIdentifierChar(char character)
        => (character >= 'A' && character <= 'Z') ||
           (character >= 'a' && character <= 'z') ||
           character == '_' || character == '$' || character >= '\u00A0';

    // validIdentCharRE = /[\.\?\w$\xA0-￿]/
    private static bool IsValidIdentifierChar(char character)
        => character == '.' || character == '?' || character == '$' ||
           (character >= 'A' && character <= 'Z') ||
           (character >= 'a' && character <= 'z') ||
           (character >= '0' && character <= '9') ||
           character == '_' || character >= '\u00A0';
}
