using System.Text.RegularExpressions;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The minimal <c>v-for</c> alias tokenizer: splits <c>"(value, key, index) in source"</c> into its source
/// and value/key/index aliases. The C# port of Vue 3.5's <c>parseForExpression</c>
/// (<c>@vue/compiler-core</c> <c>parser.ts</c>), using the same <c>forAliasRE</c>/<c>forIteratorRE</c>/
/// <c>stripParensRE</c> regular expressions rather than Babel.
/// </summary>
/// <remarks>
/// This is the single documented exception to the opaque-expression rule of [V01.01.05.02]: the alias list
/// must be decomposed structurally to build the iterator function's parameter list. Alias bodies themselves
/// stay opaque; full expression/scope analysis ([V01.01.05.04]) replaces this tokenizer. Upstream performs
/// this split during parsing; this port performs it in the <c>v-for</c> transform because the [V01.01.05.01]
/// parser leaves it out.
/// </remarks>
internal static class ForExpressionParser
{
    // Upstream forAliasRE / forIteratorRE / stripParensRE. [\s\S] matches any character (incl. newlines).
    private static readonly Regex ForAliasRegex = new(@"([\s\S]*?)\s+(?:in|of)\s+(\S[\s\S]*)", RegexOptions.Compiled);
    private static readonly Regex ForIteratorRegex = new(@",([^,\}\]]*)(?:,([^,\}\]]*))?$", RegexOptions.Compiled);
    private static readonly Regex StripParenthesesRegex = new(@"^\(|\)$", RegexOptions.Compiled);

    /// <summary>
    /// Parses the <c>v-for</c> expression, or returns <see langword="null"/> when the alias list is malformed
    /// (upstream reports <c>X_V_FOR_MALFORMED_EXPRESSION</c>).
    /// </summary>
    /// <param name="input">The raw <c>v-for</c> expression node.</param>
    public static ForParseResult? Parse(SimpleExpressionNode input)
    {
        var location = input.Location;
        var expression = input.Content;
        var aliasMatch = ForAliasRegex.Match(expression);
        if (!aliasMatch.Success)
        {
            return null;
        }

        var leftHandSide = aliasMatch.Groups[1].Value;
        var rightHandSide = aliasMatch.Groups[2].Value;

        SimpleExpressionNode CreateAlias(string content, int offset) => new()
        {
            Content = content,
            IsStatic = false,
            ConstantType = ConstantType.NotConstant,
            Location = SubLocation(location, expression, offset, content.Length),
        };

        var source = CreateAlias(
            rightHandSide.Trim(),
            IndexOf(expression, rightHandSide, leftHandSide.Length));

        ExpressionNode? value = null;
        ExpressionNode? key = null;
        ExpressionNode? index = null;

        var valueContent = StripParenthesesRegex.Replace(leftHandSide.Trim(), string.Empty).Trim();
        var trimmedOffset = leftHandSide.IndexOf(valueContent, System.StringComparison.Ordinal);

        var iteratorMatch = ForIteratorRegex.Match(valueContent);
        if (iteratorMatch.Success)
        {
            valueContent = ForIteratorRegex.Replace(valueContent, string.Empty).Trim();

            var keyContent = iteratorMatch.Groups[1].Value.Trim();
            var keyOffset = 0;
            if (keyContent.Length > 0)
            {
                keyOffset = IndexOf(expression, keyContent, trimmedOffset + valueContent.Length);
                key = CreateAlias(keyContent, keyOffset);
            }

            if (iteratorMatch.Groups[2].Success)
            {
                var indexContent = iteratorMatch.Groups[2].Value.Trim();
                if (indexContent.Length > 0)
                {
                    index = CreateAlias(
                        indexContent,
                        IndexOf(
                            expression,
                            indexContent,
                            key is not null ? keyOffset + keyContent.Length : trimmedOffset + valueContent.Length));
                }
            }
        }

        if (valueContent.Length > 0)
        {
            value = CreateAlias(valueContent, trimmedOffset);
        }

        return new ForParseResult(source, value, key, index);
    }

    private static int IndexOf(string source, string value, int startIndex)
    {
        var found = source.IndexOf(value, System.Math.Max(0, startIndex), System.StringComparison.Ordinal);
        return found < 0 ? 0 : found;
    }

    // Builds a sub-location within the expression by advancing the expression's start position over the
    // skipped prefix. Line/column stay approximate for multi-line aliases; [V01.01.05.04] recomputes these.
    private static SourceLocation SubLocation(SourceLocation expressionLocation, string expression, int offset, int length)
    {
        var start = Advance(expressionLocation.Start, expression, 0, offset);
        var end = Advance(start, expression, offset, length);
        var slice = offset >= 0 && offset + length <= expression.Length
            ? expression.Substring(offset, length)
            : string.Empty;
        return new SourceLocation(start, end, slice);
    }

    private static Position Advance(Position from, string source, int start, int count)
    {
        var line = from.Line;
        var column = from.Column;
        var lastNewLine = -1;
        var end = System.Math.Min(source.Length, start + count);
        for (var i = start; i < end; i++)
        {
            if (source[i] == '\n')
            {
                line++;
                lastNewLine = i;
            }
        }

        column = lastNewLine == -1 ? column + count : end - lastNewLine;
        return new Position(from.Offset + count, line, column);
    }
}
