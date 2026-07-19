using Shouldly;

namespace Assimalign.Viu.Syntax.Css;

// Shared helpers for the CSS parser tests: a one-line parse-to-stylesheet, and the exact-slice invariant
// walker that every located node in the tree must satisfy (SourceLocation.Source equals the exact source
// substring between its offsets), pinned recursively rather than offset-by-offset.
internal static class CssTestHelpers
{
    public static CssStylesheetNode ParseStylesheet(string source)
    {
        var result = new CssSyntaxParser().ParseSyntax(source);
        return (CssStylesheetNode)result.Nodes[0];
    }

    public static string Scope(string source, string scopeId = "data-v-test")
        => CssScopedRewriter.Rewrite(ParseStylesheet(source), scopeId);

    // Asserts the exact-slice invariant on a node and all its descendants.
    public static void AssertExactSlice(CssSyntaxNode node, string source)
    {
        var start = node.Location.Start.Offset;
        var end = node.Location.End.Offset;
        start.ShouldBeLessThanOrEqualTo(end);
        end.ShouldBeLessThanOrEqualTo(source.Length);
        node.Location.Source.ShouldBe(source.Substring(start, end - start));

        switch (node)
        {
            case CssStylesheetNode stylesheet:
                foreach (var rule in stylesheet.Rules)
                {
                    AssertExactSlice(rule, source);
                }

                break;
            case CssQualifiedRuleNode qualified:
                AssertExactSlice(qualified.Selectors, source);
                foreach (var declaration in qualified.Declarations)
                {
                    AssertExactSlice(declaration, source);
                }

                break;
            case CssAtRuleNode atRule:
                foreach (var child in atRule.Body)
                {
                    AssertExactSlice(child, source);
                }

                break;
            case CssKeyframeRuleNode keyframe:
                foreach (var declaration in keyframe.Declarations)
                {
                    AssertExactSlice(declaration, source);
                }

                break;
            case CssSelectorListNode list:
                foreach (var complex in list.Selectors)
                {
                    AssertExactSlice(complex, source);
                }

                break;
            case CssComplexSelectorNode complex:
                foreach (var part in complex.Parts)
                {
                    AssertExactSlice(part, source);
                }

                break;
            case CssPseudoSelectorNode { Argument: { } argument }:
                AssertExactSlice(argument, source);
                break;
        }
    }
}
