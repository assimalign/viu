using System;
using System.Globalization;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The <c>v-memo</c> transform: wraps the element's compiled subtree in a <c>withMemo</c> call carrying the
/// dependency expression and a per-instance cache index. The C# port of Vue 3.5's <c>transformMemo</c>
/// (<c>@vue/compiler-core</c> <c>transforms/vMemo.ts</c>). See
/// https://vuejs.org/api/built-in-directives.html#v-memo.
/// </summary>
internal static class VMemoTransform
{
    /// <summary>The node transform.</summary>
    public static Action? Transform(SyntaxNode node, TransformContext context)
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
        // double handling that upstream's shared WeakSet suppresses on a single mutated node — which the
        // immutable model, producing a fresh reduced element, cannot rely on. Pinned by
        // VOnceMemoTransformTests.VMemo_WithVFor_ProducesPerItemMemoLoop.
        if (TransformUtilities.FindDirective(element, "for") is not null)
        {
            return null;
        }

        context.SeenMemo.Add(element);

        return () =>
        {
            if (context.GetCodegenNode(element) is not VNodeCall codegenNode)
            {
                return;
            }

            // A non-component subtree must be turned into a block.
            var subtree = element.ElementType != ElementType.Component
                ? TransformUtilities.ConvertToBlock(codegenNode, context)
                : codegenNode;

            var memoCall = Ir.CallExpression(
                context.Helper(HelperNames.WithMemo),
                new object[]
                {
                    directive.Expression!,
                    Ir.FunctionExpression(null, subtree),
                    "_cache",
                    context.CacheCount.ToString(CultureInfo.InvariantCulture),
                });

            context.SetCodegenNode(element, memoCall);
            context.AppendEmptyCacheSlot();
        };
    }
}
