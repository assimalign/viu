using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The <c>v-if</c>/<c>v-else-if</c>/<c>v-else</c> transform: groups adjacent conditional siblings into one
/// <see cref="WorkingIf"/> with ordered branches and compiles it to a conditional-expression chain where each
/// branch is its own block with a stable synthetic key. The C# port of Vue 3.5's <c>transformIf</c> and
/// <c>processIf</c> (<c>@vue/compiler-core</c> <c>transforms/vIf.ts</c>).
/// See https://vuejs.org/guide/essentials/conditional.html.
/// </summary>
internal static class VIfTransform
{
    /// <summary>The node transform (built via the structural directive factory).</summary>
    public static readonly NodeTransform Transform = StructuralDirectiveFactory.Create(
        static name => name is "if" or "else" or "else-if",
        (element, directive, context) => ProcessIf(element, directive, context, (workingIf, branch, isRoot) =>
        {
            // #1587: key increments over sibling conditional chains rendered at the same depth. Computed on
            // entry (siblings are intact here); the exit callback closes over the captured key.
            var siblings = context.CurrentChildren!;
            var startIndex = ReferenceIndexOf(siblings, workingIf);
            var key = 0;
            for (var i = startIndex - 1; i >= 0; i--)
            {
                if (siblings[i] is WorkingIf sibling)
                {
                    key += sibling.Branches.Count;
                }
            }

            return () =>
            {
                if (isRoot)
                {
                    workingIf.CodegenNode = CreateCodegenNodeForBranch(branch, key, context);
                }
                else
                {
                    var alternate = CreateCodegenNodeForBranch(branch, key + workingIf.Branches.Count - 1, context);
                    workingIf.CodegenNode = AppendAlternate(workingIf.CodegenNode!, alternate);
                }
            };
        }));

    private static Action? ProcessIf(
        ElementNode element,
        DirectiveNode directive,
        TransformContext context,
        Func<WorkingIf, WorkingIfBranch, bool, Action> processCodegen)
    {
        if (directive.Name != "else" &&
            (directive.Expression is null ||
             (directive.Expression is SimpleExpressionNode simple && simple.Content.Trim().Length == 0)))
        {
            var location = directive.Expression?.Location ?? element.Location;
            context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVIfNoExpression, directive.Location));
            directive = directive with { Expression = Ir.SimpleExpression("true", false, location) };
        }

        if (directive.Name == "if")
        {
            var branch = CreateIfBranch(element, directive);
            var workingIf = new WorkingIf { Location = element.Location };
            workingIf.Branches.Add(branch);
            context.ReplaceNode(workingIf);
            return processCodegen(workingIf, branch, true);
        }

        // v-else / v-else-if: locate the adjacent v-if. The current element sits at context.ChildIndex; its
        // reduced copy (with v-else removed) is not itself in the sibling list, so we scan from the index.
        var siblings = context.CurrentChildren!;
        var comments = new List<TemplateSyntaxNode>();
        var index = context.ChildIndex;
        while (true)
        {
            index--;
            if (index < -1)
            {
                break;
            }

            var sibling = index >= 0 && index < siblings.Count ? siblings[index] : null;
            if (sibling is CommentNode)
            {
                context.RemoveNode(sibling);
                comments.Insert(0, sibling);
                continue;
            }

            if (sibling is TextNode text && text.Content.Trim().Length == 0)
            {
                context.RemoveNode(sibling);
                continue;
            }

            if (sibling is WorkingIf adjacent)
            {
                if (directive.Name == "else-if" &&
                    adjacent.Branches[adjacent.Branches.Count - 1].Condition is null)
                {
                    context.ReportError(
                        CompilerErrorFactory.Create(CompilerErrorCode.XVElseNoAdjacentIf, element.Location));
                }

                context.RemoveNode();
                var branch = CreateIfBranch(element, directive);
                if (comments.Count > 0 && !IsTransitionParent(context.Parent))
                {
                    branch.Children.InsertRange(0, comments);
                }

                if (branch.UserKey is not null)
                {
                    foreach (var existing in adjacent.Branches)
                    {
                        if (IsSameKey(existing.UserKey, branch.UserKey))
                        {
                            context.ReportError(
                                CompilerErrorFactory.Create(CompilerErrorCode.XVIfSameKey, branch.UserKey.Location));
                        }
                    }
                }

                adjacent.Branches.Add(branch);
                var onExit = processCodegen(adjacent, branch, false);
                TransformTraversal.TraverseNode(branch, context);
                onExit();
                context.CurrentNode = null;
            }
            else
            {
                context.ReportError(CompilerErrorFactory.Create(CompilerErrorCode.XVElseNoAdjacentIf, element.Location));
            }

            break;
        }

        return null;
    }

    private static WorkingIfBranch CreateIfBranch(ElementNode element, DirectiveNode directive)
    {
        var isTemplateIf = element.ElementType == ElementType.Template;
        var branch = new WorkingIfBranch
        {
            Location = element.Location,
            Condition = directive.Name == "else" ? null : directive.Expression,
            UserKey = TransformUtilities.FindProperty(element, "key"),
            IsTemplateIf = isTemplateIf,
        };

        if (isTemplateIf && TransformUtilities.FindDirective(element, "for") is null)
        {
            foreach (var child in element.Children)
            {
                branch.Children.Add(child);
            }
        }
        else
        {
            branch.Children.Add(element);
        }

        return branch;
    }

    private static TemplateSyntaxNode CreateCodegenNodeForBranch(WorkingIfBranch branch, int keyIndex, TransformContext context)
    {
        if (branch.Condition is not null)
        {
            return Ir.ConditionalExpression(
                branch.Condition,
                CreateChildrenCodegenNode(branch, keyIndex, context),
                Ir.CallExpression(context.Helper(HelperNames.CreateComment), new object[] { "\"v-if\"", "true" }));
        }

        return CreateChildrenCodegenNode(branch, keyIndex, context);
    }

    private static TemplateSyntaxNode CreateChildrenCodegenNode(WorkingIfBranch branch, int keyIndex, TransformContext context)
    {
        var keyProperty = Ir.ObjectProperty(
            "key",
            Ir.SimpleExpression(keyIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), false, Ir.LocationStub, ConstantType.CanCache));
        var children = branch.Children;
        var firstChild = children[0];
        var needFragmentWrapper = children.Count != 1 || firstChild is not ElementNode;

        if (needFragmentWrapper)
        {
            if (children.Count == 1 && firstChild is WorkingFor forChild)
            {
                var forCodegen = context.GetCodegenNode(forChild)!;
                var injected = TransformUtilities.InjectProperty(forCodegen, keyProperty, context);
                forChild.CodegenNode = injected;
                return injected;
            }

            var patchFlag = PatchFlags.StableFragment;
            if (!branch.IsTemplateIf && CountNonComment(children) == 1)
            {
                patchFlag |= PatchFlags.DevRootFragment;
            }

            return context.CreateVNodeCall(
                context.Helper(HelperNames.Fragment),
                Ir.ObjectExpression(new[] { keyProperty }),
                TransformFreeze.FreezeChildren(children),
                patchFlag,
                null,
                null,
                isBlock: true,
                disableTracking: false,
                isComponent: false,
                branch.Location);
        }

        var codegen = context.GetCodegenNode(firstChild)!;
        var inner = TransformUtilities.GetMemoedVNodeCall(codegen);
        var isMemo = !ReferenceEquals(inner, codegen);

        var vnodeCall = inner;
        if (vnodeCall is VNodeCall block)
        {
            vnodeCall = TransformUtilities.ConvertToBlock(block, context);
        }

        var withKey = TransformUtilities.InjectProperty(vnodeCall, keyProperty, context);
        if (!isMemo)
        {
            return withKey;
        }

        // memo-wrapped: rebuild the memo's inner function return.
        var memo = (CallExpression)codegen;
        var factory = (FunctionExpression)memo.Arguments[1];
        return memo with { Arguments = Replace(memo.Arguments, 1, factory with { Returns = withKey }) };
    }

    private static TemplateSyntaxNode AppendAlternate(TemplateSyntaxNode chain, TemplateSyntaxNode newAlternate)
    {
        var parentCondition = GetParentCondition(chain);
        return ReplaceAlternate(chain, parentCondition, newAlternate);
    }

    private static ConditionalExpression GetParentCondition(TemplateSyntaxNode node)
    {
        while (true)
        {
            if (node is ConditionalExpression conditional)
            {
                if (conditional.Alternate is ConditionalExpression)
                {
                    node = conditional.Alternate;
                }
                else
                {
                    return conditional;
                }
            }
            else if (node is CacheExpression cache)
            {
                node = cache.Value;
            }
            else
            {
                throw new InvalidOperationException("Unexpected node in v-if conditional chain.");
            }
        }
    }

    // Rebuilds the immutable chain, replacing the alternate of the identified parent condition.
    private static TemplateSyntaxNode ReplaceAlternate(TemplateSyntaxNode node, ConditionalExpression target, TemplateSyntaxNode newAlternate)
    {
        switch (node)
        {
            case ConditionalExpression conditional when ReferenceEquals(conditional, target):
                return conditional with { Alternate = newAlternate };
            case ConditionalExpression conditional:
                return conditional with { Alternate = ReplaceAlternate(conditional.Alternate, target, newAlternate) };
            case CacheExpression cache:
                return cache with { Value = ReplaceAlternate(cache.Value, target, newAlternate) };
            default:
                return node;
        }
    }

    private static bool IsSameKey(PropertyNode? left, PropertyNode? right)
    {
        if (left is null || right is null || left.GetType() != right.GetType())
        {
            return false;
        }

        if (left is AttributeNode leftAttribute && right is AttributeNode rightAttribute)
        {
            return leftAttribute.Value?.Content == rightAttribute.Value?.Content;
        }

        if (left is DirectiveNode leftDirective && right is DirectiveNode rightDirective)
        {
            if (leftDirective.Expression is not SimpleExpressionNode leftExpression ||
                rightDirective.Expression is not SimpleExpressionNode rightExpression)
            {
                return false;
            }

            return leftExpression.IsStatic == rightExpression.IsStatic &&
                   leftExpression.Content == rightExpression.Content;
        }

        return false;
    }

    private static bool IsTransitionParent(TemplateSyntaxNode? parent)
        => parent is ElementNode { Tag: "transition" or "Transition" };

    private static int CountNonComment(IReadOnlyList<TemplateSyntaxNode> children)
    {
        var count = 0;
        foreach (var child in children)
        {
            if (child is not CommentNode)
            {
                count++;
            }
        }

        return count;
    }

    private static int ReferenceIndexOf(IReadOnlyList<TemplateSyntaxNode> list, TemplateSyntaxNode node)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (ReferenceEquals(list[index], node))
            {
                return index;
            }
        }

        return -1;
    }

    private static SyntaxList<object> Replace(SyntaxList<object> arguments, int index, object value)
    {
        var array = new object[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            array[i] = i == index ? value : arguments[i];
        }

        return new SyntaxList<object>(array);
    }
}
