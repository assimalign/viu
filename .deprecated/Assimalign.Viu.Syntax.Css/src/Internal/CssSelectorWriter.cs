using System.Text;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Renders a <see cref="CssSelectorListNode"/> back to its canonical selector text, in the same
/// two-space-normalized form <see cref="CssStylesheetWriter"/> emits a rule prelude in. Used by
/// <see cref="CssSyntaxFactory"/> to fill a constructed <see cref="CssQualifiedRuleNode.Prelude"/> from its
/// parts so the node is internally consistent (its prelude text and parsed <see cref="CssQualifiedRuleNode.Selectors"/>
/// agree) and deterministic — two rules built from equal selector lists carry equal preludes.
/// </summary>
/// <remarks>
/// The prelude is <em>not</em> what the serializers emit — <see cref="CssStylesheetWriter"/> and
/// <see cref="CssScopedRewriter"/> both render a rule's selector from its parsed parts, never from the raw
/// prelude — so this renderer's output only has to match the parts (which it does, sharing the same
/// part-walking logic) and stay deterministic; it is deliberately independent of the serializers so this
/// construction-only path can never perturb them.
/// </remarks>
internal static class CssSelectorWriter
{
    /// <summary>Renders <paramref name="list"/> to canonical selector text.</summary>
    /// <param name="list">The selector list to render.</param>
    /// <returns>The comma-joined complex selectors.</returns>
    public static string Render(CssSelectorListNode list)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < list.Selectors.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            RenderComplexSelector(list.Selectors[index], builder);
        }

        return builder.ToString();
    }

    private static void RenderComplexSelector(CssComplexSelectorNode complex, StringBuilder builder)
    {
        foreach (var part in complex.Parts)
        {
            switch (part)
            {
                case CssCombinatorNode combinator:
                    builder.Append(RenderCombinator(combinator.Combinator));
                    break;
                case CssSimpleSelectorNode simple:
                    builder.Append(simple.Text);
                    break;
                case CssPseudoSelectorNode pseudo:
                    builder.Append(pseudo.Location.Source);
                    break;
            }
        }
    }

    private static string RenderCombinator(CssCombinatorKind kind)
        => kind switch
        {
            CssCombinatorKind.Child => " > ",
            CssCombinatorKind.NextSibling => " + ",
            CssCombinatorKind.SubsequentSibling => " ~ ",
            _ => " ",
        };
}
