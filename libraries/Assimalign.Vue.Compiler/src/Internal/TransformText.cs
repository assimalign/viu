using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The text transform: merges adjacent text and interpolation children into a single compound expression and
/// pre-converts text children to <c>createTextVNode</c> calls so the runtime skips normalization. The C# port
/// of Vue 3.5's <c>transformText</c> (<c>@vue/compiler-core</c> <c>transforms/transformText.ts</c>).
/// </summary>
internal static class TransformText
{
    /// <summary>The node transform (runs on exit so child expressions are already processed).</summary>
    public static Action? Transform(SyntaxNode node, TransformContext context)
    {
        var children = ResolveChildren(node, context);
        if (children is null)
        {
            return null;
        }

        return () =>
        {
            List<object>? containerParts = null;
            var containerIndex = 0;
            var hasText = false;

            for (var i = 0; i < children.Count; i++)
            {
                if (!IsText(children[i]))
                {
                    continue;
                }

                hasText = true;
                for (var j = i + 1; j < children.Count;)
                {
                    var next = children[j];
                    if (IsText(next))
                    {
                        if (containerParts is null)
                        {
                            containerParts = new List<object> { children[i] };
                            containerIndex = i;
                            children[i] = Ir.CompoundExpression(containerParts.ToArray());
                        }

                        containerParts.Add(" + ");
                        containerParts.Add(next);
                        children[containerIndex] = Ir.CompoundExpression(containerParts.ToArray());
                        children.RemoveAt(j);
                    }
                    else
                    {
                        containerParts = null;
                        break;
                    }
                }
            }

            if (!hasText || (children.Count == 1 && IsFastPathTextParent(node, context)))
            {
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!IsText(child) && child is not CompoundExpressionNode)
                {
                    continue;
                }

                var callArguments = new List<object>();
                if (!(child is TextNode { Content: " " }))
                {
                    callArguments.Add(child);
                }

                if (!context.Ssr && ConstantAnalysis.GetConstantType(child) == ConstantType.NotConstant)
                {
                    callArguments.Add(((int)PatchFlags.Text).ToString(CultureInfo.InvariantCulture));
                }

                children[i] = new TextCallNode
                {
                    Content = child,
                    CodegenNode = Ir.CallExpression(context.Helper(HelperNames.CreateText), callArguments),
                    Location = child.Location,
                };
            }
        };
    }

    private static List<SyntaxNode>? ResolveChildren(SyntaxNode node, TransformContext context) => node switch
    {
        RootNode root => context.WorkingChildrenOf(root, root.Children),
        ElementNode element => context.WorkingChildrenOf(element, element.Children),
        WorkingFor workingFor => workingFor.Children,
        WorkingIfBranch branch => branch.Children,
        _ => null,
    };

    private static bool IsText(SyntaxNode node) => node is TextNode or InterpolationNode;

    private static bool IsFastPathTextParent(SyntaxNode node, TransformContext context)
    {
        if (node is RootNode)
        {
            return true;
        }

        if (node is ElementNode { ElementType: ElementType.Element } element)
        {
            foreach (var property in element.Properties)
            {
                if (property is DirectiveNode directive && !context.DirectiveTransforms.ContainsKey(directive.Name))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
}
