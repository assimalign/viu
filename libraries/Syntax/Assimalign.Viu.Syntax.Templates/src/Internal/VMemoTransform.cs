using System;
using System.Globalization;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <c>v-memo</c> transform: wraps the element's compiled subtree in a <c>withMemo</c> call carrying the
/// dependency expression and a per-instance cache index.
/// </summary>
internal static class VMemoTransform
{
    /// <summary>The node transform.</summary>
    public static Action? Transform(TemplateSyntaxNode node, TransformContext context)
    {
        if (node is not ElementNode element)
        {
            return null;
        }

        var directive = TransformUtilities.FindDirective(element, "memo");
        if (directive is null || context.SeenMemo.Contains(element))
        {
            return null;
        }

        // v-memo on a v-for element is memoized per item by the v-for transform's render-list loop, not by a
        // whole-subtree withMemo. This guard skips the outer element that still carries v-for; the v-for
        // transform separately marks its reduced inner element as seen (SeenMemo). Together they prevent the
        // double handling. Node identity cannot carry the mark, because the transform produces a fresh
        // reduced element rather than mutating one node. Pinned by
        // VOnceMemoTransformTests.VMemo_WithVFor_ProducesPerItemMemoLoop.
        if (TransformUtilities.FindDirective(element, "for") is not null)
        {
            return null;
        }

        context.SeenMemo.Add(element);

        return () =>
        {
            // Identifier rewriting can replace the immutable element record after this transform's entry
            // callback ran. Resolve the final current record on exit so the memo wrapper encloses the
            // code-generation node installed for that record and carries its rewritten dependencies.
            ElementNode target = context.CurrentNode as ElementNode ?? element;
            DirectiveNode targetDirective = TransformUtilities.FindDirective(target, "memo") ?? directive;
            if (context.GetCodegenNode(target) is not VirtualNodeCall codegenNode)
            {
                return;
            }

            // A non-component subtree must be turned into a block.
            var subtree = target.ElementType != ElementType.Component
                ? TransformUtilities.ConvertToBlock(codegenNode, context)
                : codegenNode;

            var memoCall = Ir.CallExpression(
                context.Helper(HelperNames.WithMemo),
                new object[]
                {
                    targetDirective.Expression!,
                    Ir.FunctionExpression(null, subtree),
                    "_cache",
                    context.CacheCount.ToString(CultureInfo.InvariantCulture),
                });

            context.SetCodegenNode(target, memoCall);
            context.AppendEmptyCacheSlot();
        };
    }
}
