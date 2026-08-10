using System;
using System.Threading;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Css;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Resolves a class token in template markup to the declaration authored in this component's style
/// blocks ([V01.01.12.07.16], #337). Parsing uses the shared CSS syntax tree, so hover follows the
/// same selector grammar as scoped-style and CSS-module compilation.
/// </summary>
internal static class StyleClassHoverProvider
{
    private static readonly CssSyntaxParser Parser = new();

    /// <summary>Gets the first source-order declaration for a template class token.</summary>
    /// <param name="document">The immutable live document.</param>
    /// <param name="context">The class-attribute token context.</param>
    /// <param name="cancellationToken">The token cancelling CSS parsing.</param>
    /// <returns>The authored declaration hover, or <see langword="null"/> when none is declared.</returns>
    internal static LanguageHover? GetHover(
        LanguageDocument document,
        UtilityClassCompletionContext context,
        CancellationToken cancellationToken)
    {
        if (context.TokenText.Length == 0)
        {
            return null;
        }

        foreach (var style in document.Syntax.Styles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (style.Lang is not null &&
                !string.Equals(style.Lang, "css", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parse = Parser.ParseSyntax(style.Content, cancellationToken);
            if (parse.Nodes.Count == 0 ||
                parse.Nodes[0] is not CssStylesheetNode stylesheet)
            {
                continue;
            }

            var rule = FindRule(stylesheet.Rules, context.TokenText);
            if (rule is null)
            {
                continue;
            }

            return new LanguageHover(
                $"**`.{context.TokenText}`** — declared in this component's style block.\n\n" +
                    $"```css\n{rule.Location.Source.Trim()}\n```",
                new LanguageRange(
                    TextCoordinateConverter.GetPosition(document.Text, context.TokenStart),
                    TextCoordinateConverter.GetPosition(document.Text, context.TokenEnd)));
        }

        return null;
    }

    private static CssQualifiedRuleNode? FindRule(
        SyntaxList<CssSyntaxNode> nodes,
        string className)
    {
        foreach (var node in nodes)
        {
            if (node is CssQualifiedRuleNode rule &&
                ContainsClass(rule.Selectors, className))
            {
                return rule;
            }

            if (node is CssAtRuleNode atRule)
            {
                var nested = FindRule(atRule.Body, className);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool ContainsClass(
        CssSelectorListNode selectors,
        string className)
    {
        foreach (var selector in selectors.Selectors)
        {
            foreach (var part in selector.Parts)
            {
                if (part is CssSimpleSelectorNode
                    {
                        Selector: CssSimpleSelectorKind.Class,
                        Text: var text,
                    } &&
                    string.Equals(text, "." + className, StringComparison.Ordinal))
                {
                    return true;
                }

                if (part is CssPseudoSelectorNode { Argument: { } argument } &&
                    ContainsClass(argument, className))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
