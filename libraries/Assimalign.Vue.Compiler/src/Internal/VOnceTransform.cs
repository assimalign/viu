using System;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The <c>v-once</c> transform: marks the element's subtree as rendered once and cached, and pauses block
/// tracking around it. The C# port of Vue 3.5's <c>transformOnce</c> (<c>@vue/compiler-core</c>
/// <c>transforms/vOnce.ts</c>). See https://vuejs.org/api/built-in-directives.html#v-once.
/// </summary>
internal static class VOnceTransform
{
    /// <summary>The node transform.</summary>
    public static Action? Transform(SyntaxNode node, TransformContext context)
    {
        if (node is not ElementNode element || TransformUtilities.FindDirective(element, "once", allowEmpty: true) is null)
        {
            return null;
        }

        if (context.SeenOnce.Contains(element) || context.InVOnce || context.InSSR)
        {
            return null;
        }

        context.SeenOnce.Add(element);
        context.InVOnce = true;
        context.Helper(HelperNames.SetBlockTracking);

        return () =>
        {
            context.InVOnce = false;
            var current = context.CurrentNode;
            if (current is null)
            {
                return;
            }

            var codegenNode = context.GetCodegenNode(current);
            if (codegenNode is not null)
            {
                context.SetCodegenNode(current, context.Cache(codegenNode, isVNode: true, inVOnce: true));
            }
        };
    }
}
