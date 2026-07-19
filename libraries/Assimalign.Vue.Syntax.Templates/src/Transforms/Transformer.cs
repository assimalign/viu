using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The template transform entry point: runs the ordered node and directive transforms over a parsed
/// <see cref="RootNode"/> and produces the render-function code-generation tree. The C# port of Vue 3.5's
/// <c>transform()</c> plus the base preset from <c>getBaseTransformPreset</c> (<c>@vue/compiler-core</c>
/// <c>transform.ts</c>/<c>compile.ts</c>), merged with the DOM directive transforms.
/// </summary>
/// <remarks>
/// This type has no instance state; each call is a fresh, deterministic, pure transform over its input,
/// producing immutable, value-equatable output suitable for incremental-generator caching.
/// </remarks>
public static class Transformer
{
    /// <summary>Transforms <paramref name="root"/> using the default (DOM) transform set.</summary>
    /// <param name="root">The parsed template root.</param>
    public static TransformResult Transform(RootNode root) => Transform(root, new TransformOptions());

    /// <summary>Transforms <paramref name="root"/> with the given <paramref name="options"/>.</summary>
    /// <param name="root">The parsed template root.</param>
    /// <param name="options">The transform configuration.</param>
    public static TransformResult Transform(RootNode root, TransformOptions options)
    {
        var nodeTransforms = BuildNodeTransforms(options);
        var directiveTransforms = BuildDirectiveTransforms(options);
        var context = new TransformContext(root, nodeTransforms, directiveTransforms, options);

        TransformTraversal.TraverseNode(root, context);
        if (options.HoistStatic)
        {
            // The static-caching/stringification pass ([V01.01.05.07]): cache fully static subtrees
            // (marking them PatchFlags.Cached) and collapse contiguous static runs into stringified static
            // vnodes, before the root code-generation node is built (upstream runs cacheStatic here).
            StaticCache.Cache(root, context);
        }

        var rootChildren = context.WorkingChildrenOf(root, root.Children);
        var codegenNode = CreateRootCodegen(context, rootChildren);

        return new TransformResult(
            context,
            codegenNode,
            TransformFreeze.FreezeChildren(rootChildren),
            ToArray(context.HelperKeys),
            ToArray(context.ComponentNames),
            ToArray(context.DirectiveNames),
            context.Hoists,
            context.CachedSlots);
    }

    // The base preset order: structural transforms, then (when prefixing) expression rewriting, then slot
    // outlet, element, slot-scope tracking, and text. transformExpression slots in before transformSlotOutlet,
    // exactly as upstream inserts it in getBaseTransformPreset when !__BROWSER__ && prefixIdentifiers.
    private static IReadOnlyList<NodeTransform> BuildNodeTransforms(TransformOptions options)
    {
        var transforms = new List<NodeTransform>
        {
            VOnceTransform.Transform,
            VIfTransform.Transform,
            VMemoTransform.Transform,
            VForTransform.Transform,
        };

        if (options.PrefixIdentifiers)
        {
            transforms.Add(TransformExpression.Transform);
        }

        transforms.Add(TransformSlotOutlet.Transform);
        transforms.Add(TransformElement.Transform);
        transforms.Add(VSlotTransform.TrackSlotScopes);
        transforms.Add(TransformText.Transform);
        transforms.AddRange(options.NodeTransforms);
        return transforms;
    }

    private static IReadOnlyDictionary<string, DirectiveTransform> BuildDirectiveTransforms(TransformOptions options)
    {
        var transforms = new Dictionary<string, DirectiveTransform>();
        foreach (var entry in DomDirectiveTransforms.Create())
        {
            transforms[entry.Key] = entry.Value;
        }

        foreach (var entry in options.DirectiveTransforms)
        {
            transforms[entry.Key] = entry.Value;
        }

        return transforms;
    }

    // Port of createRootCodegen.
    private static TemplateSyntaxNode? CreateRootCodegen(TransformContext context, List<TemplateSyntaxNode> children)
    {
        if (children.Count == 1)
        {
            var child = children[0];
            if (child is ElementNode { ElementType: ElementType.Element or ElementType.Component } element &&
                element.ElementType != ElementType.Slot &&
                context.GetCodegenNode(element) is { } elementCodegen)
            {
                if (elementCodegen is VNodeCall vnodeCall)
                {
                    var block = TransformUtilities.ConvertToBlock(vnodeCall, context);
                    context.SetCodegenNode(element, block);
                    return block;
                }

                return elementCodegen;
            }

            // Single slot outlet, IfNode, ForNode, or text: use its own codegen node, or the node itself.
            return context.GetCodegenNode(child) ?? TransformFreeze.FreezeNode(child);
        }

        if (children.Count > 1)
        {
            var patchFlag = PatchFlags.StableFragment;
            if (CountNonComment(children) == 1)
            {
                patchFlag |= PatchFlags.DevRootFragment;
            }

            return context.CreateVNodeCall(
                context.Helper(HelperNames.Fragment),
                null,
                TransformFreeze.FreezeChildren(children),
                patchFlag,
                null,
                null,
                isBlock: true,
                disableTracking: false,
                isComponent: false);
        }

        return null;
    }

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

    private static IReadOnlyList<T> ToArray<T>(IReadOnlyCollection<T> source)
    {
        var array = new T[source.Count];
        var index = 0;
        foreach (var item in source)
        {
            array[index++] = item;
        }

        return array;
    }
}
