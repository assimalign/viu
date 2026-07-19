using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The static-caching pass — the C# port of Vue 3.5's <c>cacheStatic</c> (<c>@vue/compiler-core</c>
/// <see href="https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/transforms/cacheStatic.ts">transforms/cacheStatic.ts</see>,
/// the successor to <c>hoistStatic</c>). It runs once after the transform traversal and before the root
/// code-generation node is built: it walks the tree, and every fully static subtree at or above
/// <see cref="ConstantType.CanCache"/> is marked <see cref="PatchFlags.Cached"/> (so the runtime diff skips
/// it) and wrapped in a render-cache slot (created once per instance, reused across re-renders); an element
/// whose children are dynamic but whose props are static gets its props cached the same way. On WASM each
/// avoided vnode creation and patch visit is a JS-interop round-trip saved, which is why this is a primary
/// optimization here. The DOM stringification pass (<see cref="StaticStringifier"/>) then runs over the same
/// children.
/// </summary>
/// <remarks>
/// <para>
/// Deliberate divergences from upstream, pinned by tests (see <c>docs/DESIGN.md</c>):
/// </para>
/// <list type="bullet">
/// <item>Upstream splits static optimization between <c>context.cache</c> (vnode subtrees, per-instance
/// <c>_cache</c>) and <c>context.hoist</c> (props objects, per-type module consts). Viu routes both through
/// the per-instance <c>_cache</c> seam the emitter already implements, because the C# generator model has no
/// module-const scope without a new field-emission contract; each value is still created once per instance
/// and reused across re-renders.</item>
/// <item>The whole-children-array cache (upstream <c>cachedAsArray</c>) and <c>TEXT_CALL</c> caching are
/// omitted; each eligible static sibling is cached individually. A fully static text run is a single
/// <see cref="TextNode"/> folded into its element's cached subtree, so the <c>TEXT_CALL</c> path is
/// unreachable in practice.</item>
/// </list>
/// </remarks>
internal static class StaticCache
{
    /// <summary>Runs the static-caching walk over <paramref name="root"/> (upstream <c>cacheStatic</c>).</summary>
    /// <param name="root">The transformed template root.</param>
    /// <param name="context">The transform context.</param>
    public static void Cache(RootNode root, TransformContext context)
    {
        // The root itself is never cached: a single root element may receive parent fallthrough attributes,
        // so upstream passes doNotHoistNode for it (isSingleElementRoot).
        Walk(root, context, IsSingleElementRoot(root, context));
    }

    private static bool IsSingleElementRoot(RootNode root, TransformContext context)
    {
        var children = context.WorkingChildrenOf(root, root.Children);
        return children.Count == 1 &&
               children[0] is ElementNode { ElementType: not ElementType.Slot };
    }

    private static void Walk(TemplateSyntaxNode node, TransformContext context, bool doNotHoistNode)
    {
        var children = ChildrenOf(node, context);
        if (children is null)
        {
            return;
        }

        var toCache = new List<ElementNode>();
        foreach (var child in children)
        {
            // Only plain elements are eligible for caching (upstream also caches text calls; see the type
            // remarks for why that path is unreachable in Viu).
            if (child is ElementNode { ElementType: ElementType.Element } element)
            {
                var constantType = doNotHoistNode
                    ? ConstantType.NotConstant
                    : ConstantAnalysis.GetConstantType(element, context);
                if (constantType > ConstantType.NotConstant)
                {
                    if (constantType >= ConstantType.CanCache)
                    {
                        if (context.GetCodegenNode(element) is VNodeCall codegenNode)
                        {
                            context.SetCodegenNode(element, codegenNode with { PatchFlag = PatchFlags.Cached });
                        }

                        toCache.Add(element);
                        continue;
                    }
                }
                else
                {
                    // The element may have dynamic children, but its props can still be eligible for caching.
                    if (context.GetCodegenNode(element) is VNodeCall { Props: { } props } codegenNode)
                    {
                        var flag = codegenNode.PatchFlag;
                        if ((flag is null or PatchFlags.NeedPatch or PatchFlags.Text) &&
                            ConstantAnalysis.GetGeneratedPropsConstantType(element, context) >= ConstantType.CanCache)
                        {
                            context.SetCodegenNode(element, codegenNode with { Props = context.Cache(props) });
                        }
                    }
                }
            }

            // Walk further (caching descendants inside components/loops/conditionals via the side table).
            switch (child)
            {
                case ElementNode descendant:
                    var isComponent = descendant.ElementType == ElementType.Component;
                    if (isComponent)
                    {
                        context.ScopeVSlot++;
                    }

                    Walk(descendant, context, doNotHoistNode: false);
                    if (isComponent)
                    {
                        context.ScopeVSlot--;
                    }

                    break;
                case WorkingFor workingFor:
                    // A single v-for child stays a block, so do not hoist it.
                    Walk(workingFor, context, doNotHoistNode: workingFor.Children.Count == 1);
                    break;
                case ForNode forNode:
                    Walk(forNode, context, doNotHoistNode: forNode.Children.Count == 1);
                    break;
                case WorkingIf workingIf:
                    foreach (var branch in workingIf.Branches)
                    {
                        Walk(branch, context, doNotHoistNode: branch.Children.Count == 1);
                    }

                    break;
                case IfNode ifNode:
                    foreach (var branch in ifNode.Branches)
                    {
                        Walk(branch, context, doNotHoistNode: branch.Children.Count == 1);
                    }

                    break;
            }
        }

        // Cache each eligible static subtree into its own render-cache slot: child.codegenNode = cache(...).
        foreach (var element in toCache)
        {
            if (context.GetCodegenNode(element) is { } codegenNode)
            {
                context.SetCodegenNode(element, context.Cache(codegenNode));
            }
        }

        // The DOM stringification hook (upstream context.transformHoist), gated to non-SSR: contiguous
        // stringifiable cached runs above the thresholds collapse into a single static vnode.
        if (toCache.Count > 0 && !context.Ssr && !context.InSSR)
        {
            StaticStringifier.Run(node, context);
        }
    }

    // The list of children to walk. Root/element/working-for/working-if-branch expose their mutable working
    // children (shared with code generation through the side table / CreateRootCodegen); the frozen forms are
    // materialized read-only for caching-only recursion.
    private static IReadOnlyList<TemplateSyntaxNode>? ChildrenOf(TemplateSyntaxNode node, TransformContext context)
        => node switch
        {
            RootNode root => context.WorkingChildrenOf(root, root.Children),
            ElementNode element => context.WorkingChildrenOf(element, element.Children),
            WorkingFor workingFor => workingFor.Children,
            WorkingIfBranch workingIfBranch => workingIfBranch.Children,
            ForNode forNode => Materialize(forNode.Children),
            IfBranchNode ifBranch => Materialize(ifBranch.Children),
            _ => null,
        };

    private static List<TemplateSyntaxNode> Materialize(SyntaxList<TemplateChildNode> children)
    {
        var list = new List<TemplateSyntaxNode>(children.Count);
        foreach (var child in children)
        {
            list.Add(child);
        }

        return list;
    }
}
