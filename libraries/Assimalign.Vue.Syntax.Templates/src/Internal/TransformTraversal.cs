using System;
using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The transform traversal: applies node transforms on entry (collecting exit callbacks), recurses into
/// children, then runs the exit callbacks in reverse. The C# port of Vue 3.5's <c>traverseNode</c> and
/// <c>traverseChildren</c> (<c>@vue/compiler-core</c> <c>transform.ts</c>).
/// </summary>
internal static class TransformTraversal
{
    public static void TraverseNode(TemplateSyntaxNode node, TransformContext context)
    {
        context.CurrentNode = node;
        var exitCallbacks = new List<Action>();
        foreach (var transform in context.NodeTransforms)
        {
            var onExit = transform(node, context);
            if (onExit is not null)
            {
                exitCallbacks.Add(onExit);
            }

            if (context.CurrentNode is null)
            {
                // node was removed
                return;
            }

            node = context.CurrentNode;
        }

        switch (node)
        {
            case CommentNode:
                if (!context.Ssr)
                {
                    context.Helper(HelperNames.CreateComment);
                }

                break;
            case InterpolationNode:
                if (!context.Ssr)
                {
                    context.Helper(HelperNames.ToDisplayString);
                }

                break;
            case WorkingIf workingIf:
                foreach (var branch in workingIf.Branches)
                {
                    TraverseNode(branch, context);
                }

                break;
            case WorkingIfBranch branch:
                TraverseChildren(branch, branch.Children, context);
                break;
            case WorkingFor workingFor:
                TraverseChildren(workingFor, workingFor.Children, context);
                break;
            case ElementNode element:
                TraverseChildren(element, context.WorkingChildrenOf(element, element.Children), context);
                break;
            case RootNode root:
                TraverseChildren(root, context.WorkingChildrenOf(root, root.Children), context);
                break;
        }

        context.CurrentNode = node;
        for (var index = exitCallbacks.Count - 1; index >= 0; index--)
        {
            exitCallbacks[index]();
        }
    }

    public static void TraverseChildren(TemplateSyntaxNode parent, List<TemplateSyntaxNode> children, TransformContext context)
    {
        var i = 0;
        Action nodeRemoved = () => i--;
        for (; i < children.Count; i++)
        {
            context.Parent = parent;
            context.CurrentChildren = children;
            context.ChildIndex = i;
            context.OnNodeRemoved = nodeRemoved;
            TraverseNode(children[i], context);
        }
    }
}
