using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The DOM static-stringification pass — the C# port of Vue 3.5's <c>stringifyStatic</c>
/// (<c>@vue/compiler-dom</c>
/// <see href="https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-dom/src/transforms/stringifyStatic.ts">transforms/stringifyStatic.ts</see>,
/// upstream's <c>HoistTransform</c>). It runs at the end of the static-caching walk over a node's children:
/// contiguous runs of cached, stringifiable static siblings that reach the upstream thresholds
/// (<see cref="NodeCountThreshold"/> nodes, or <see cref="ElementWithBindingCountThreshold"/> elements with
/// attribute bindings) collapse into a single <c>createStaticVNode(html, nodeCount)</c> call carrying the
/// serialized HTML, which the runtime inserts via one <c>innerHTML</c> assignment instead of creating each
/// vnode across the JS-interop boundary. This is the founding-decision-#4 interop reducer: every avoided
/// node is a marshaling round-trip saved on WASM.
/// </summary>
/// <remarks>
/// <para>
/// Deliberate divergences from upstream, all AOT- and immutability-driven and pinned by tests
/// (see <c>docs/DESIGN.md</c>):
/// </para>
/// <list type="bullet">
/// <item>No constant-expression evaluation. Upstream evaluates constant interpolations and constant
/// <c>v-bind</c> values with <c>new Function</c>; that is forbidden by the no-dynamic-codegen rule, and in
/// Vuecs's opaque-expression model no interpolation or dynamic binding is ever classified constant, so a
/// stringifiable cached subtree only ever contains static text, comments, and static attributes.
/// <see cref="AnalyzeNode"/> bails on anything else.</item>
/// <item>Only runs on a template root's children and a plain element's children (the two containers with
/// clean immutable write-back). Static descendants inside <c>v-if</c>/<c>v-for</c> bodies are still cached,
/// just not stringified.</item>
/// <item>Merged cache slots stay reserved-but-unused rather than being compacted (upstream splices
/// <c>context.cached</c> and re-indexes), which keeps <see cref="CacheExpression"/> immutable.</item>
/// </list>
/// </remarks>
internal static class StaticStringifier
{
    // upstream StringifyThresholds (@vue/compiler-dom stringifyStatic.ts):
    //   NODE_COUNT = 20, ELEMENT_WITH_BINDING_COUNT = 5.
    private const int NodeCountThreshold = 20;
    private const int ElementWithBindingCountThreshold = 5;

    // upstream isNonStringifiable makeMap: table-section tags that innerHTML would reparent, so they are
    // never stringified.
    private static readonly HashSet<string> NonStringifiableTags = new(StringComparer.Ordinal)
    {
        "caption", "thead", "tr", "th", "tbody", "td", "tfoot", "colgroup", "col",
    };

    /// <summary>
    /// Stringifies eligible contiguous cached-static runs among <paramref name="node"/>'s children (the
    /// upstream <c>transformHoist</c> invocation at the tail of <c>walk</c>). Bails for slot content, and
    /// only rewrites a <see cref="RootNode"/>'s working children or a plain element's
    /// <see cref="VNodeCall"/> children.
    /// </summary>
    /// <param name="node">The container whose children were just cached.</param>
    /// <param name="context">The transform context.</param>
    public static void Run(TemplateSyntaxNode node, TransformContext context)
    {
        // Bail stringification for slot content (upstream: if (context.scopes.vSlot > 0) return).
        if (context.ScopeVSlot > 0)
        {
            return;
        }

        if (node is RootNode root)
        {
            // The root's working children are the same list CreateRootCodegen freezes afterwards, so an
            // in-place edit is reflected.
            StringifyChildren(context.WorkingChildrenOf(root, root.Children), context);
        }
        else if (node is ElementNode element &&
                 context.GetCodegenNode(element) is VNodeCall { Children: SyntaxList<TemplateChildNode> frozenChildren } vnode)
        {
            var children = new List<TemplateSyntaxNode>(frozenChildren.Count);
            foreach (var child in frozenChildren)
            {
                children.Add(child);
            }

            var originalCount = children.Count;
            StringifyChildren(children, context);
            if (children.Count != originalCount)
            {
                var updated = new TemplateChildNode[children.Count];
                for (var index = 0; index < children.Count; index++)
                {
                    updated[index] = (TemplateChildNode)children[index];
                }

                context.SetCodegenNode(element, vnode with { Children = new SyntaxList<TemplateChildNode>(updated) });
            }
        }
    }

    // Port of the stringifyStatic body: scan for the largest contiguous chunk of stringifiable cached
    // siblings, and collapse it once the thresholds are met.
    private static void StringifyChildren(List<TemplateSyntaxNode> children, TransformContext context)
    {
        var nodeCount = 0;
        var elementWithBindingCount = 0;
        var currentChunk = new List<ElementNode>();

        var index = 0;
        for (; index < children.Count; index++)
        {
            if (GetCachedElement(children[index], context) is { } element &&
                AnalyzeNode(element) is { } analyzed)
            {
                nodeCount += analyzed.NodeCount;
                elementWithBindingCount += analyzed.ElementWithBindingCount;
                currentChunk.Add(element);
                continue;
            }

            index -= StringifyCurrentChunk(children, index, nodeCount, elementWithBindingCount, currentChunk, context);
            nodeCount = 0;
            elementWithBindingCount = 0;
            currentChunk.Clear();
        }

        StringifyCurrentChunk(children, index, nodeCount, elementWithBindingCount, currentChunk, context);
    }

    private static int StringifyCurrentChunk(
        List<TemplateSyntaxNode> children,
        int currentIndex,
        int nodeCount,
        int elementWithBindingCount,
        List<ElementNode> currentChunk,
        TransformContext context)
    {
        if (nodeCount < NodeCountThreshold && elementWithBindingCount < ElementWithBindingCountThreshold)
        {
            return 0;
        }

        var html = new StringBuilder();
        foreach (var element in currentChunk)
        {
            StringifyNode(html, element, context);
        }

        // createStaticVNode(html, nodeCount): the html rides as a static simple expression so the emitter
        // writes it as a C#-escaped string literal; the count rides as a raw integer literal string.
        var staticCall = Ir.CallExpression(
            context.Helper(HelperNames.CreateStatic),
            new object[]
            {
                Ir.SimpleExpression(html.ToString(), isStatic: true),
                currentChunk.Count.ToString(CultureInfo.InvariantCulture),
            });

        var deleteCount = currentChunk.Count - 1;

        // Replace the first chunk node's cache value with the static call; the node stays in the list and its
        // cache slot now yields the single static vnode.
        if (context.GetCodegenNode(currentChunk[0]) is CacheExpression firstCache)
        {
            context.SetCodegenNode(currentChunk[0], firstCache with { Value = staticCall });
        }

        if (currentChunk.Count > 1)
        {
            // Remove the merged nodes (all but the first) from the children list.
            children.RemoveRange(currentIndex - currentChunk.Count + 1, deleteCount);

            // Upstream also compacts context.cached and re-indexes trailing cache expressions; Vuecs leaves
            // the merged slots reserved-but-unused to keep CacheExpression immutable (see docs/DESIGN.md).
        }

        return deleteCount;
    }

    private static ElementNode? GetCachedElement(TemplateSyntaxNode node, TransformContext context)
        => node is ElementNode { ElementType: ElementType.Element } element &&
           context.GetCodegenNode(element) is CacheExpression
            ? element
            : null;

    /// <summary>
    /// Analyzes a cached element (upstream <c>analyzeNode</c>): returns the node count and the count of
    /// elements carrying bindings inside it, or <see langword="null"/> when it is not stringifiable.
    /// </summary>
    private static (int NodeCount, int ElementWithBindingCount)? AnalyzeNode(ElementNode node)
    {
        if (NonStringifiableTags.Contains(node.Tag))
        {
            return null;
        }

        var nodeCount = 1;
        var elementWithBindingCount = node.Properties.Count > 0 ? 1 : 0;
        return WalkStringifiable(node, ref nodeCount, ref elementWithBindingCount)
            ? (nodeCount, elementWithBindingCount)
            : null;
    }

    private static bool WalkStringifiable(ElementNode node, ref int nodeCount, ref int elementWithBindingCount)
    {
        var isOptionTag = node.Tag == "option" && node.Namespace == ElementNamespace.Html;
        foreach (var property in node.Properties)
        {
            // Bail on a non-stringifiable plain attribute.
            if (property is AttributeNode attribute && !IsStringifiableAttribute(attribute.Name, node.Namespace))
            {
                return false;
            }

            if (property is DirectiveNode { Name: "bind" } directive)
            {
                // Bail on a dynamic/compound argument, or a static argument that is not a stringifiable attr.
                if (directive.Argument is CompoundExpressionNode ||
                    (directive.Argument is SimpleExpressionNode { IsStatic: true } argument &&
                     !IsStringifiableAttribute(argument.Content, node.Namespace)))
                {
                    return false;
                }

                // Bail on a compound or non-stringifiable-constant expression. In Vuecs's opaque model an
                // ordinary v-bind expression is never CAN_STRINGIFY, so every real binding bails here.
                if (directive.Expression is CompoundExpressionNode ||
                    (directive.Expression is SimpleExpressionNode expression &&
                     expression.ConstantType < ConstantType.CanStringify))
                {
                    return false;
                }

                // <option :value="1"> cannot be safely stringified.
                if (isOptionTag &&
                    TransformUtilities.IsStaticArgumentOf(directive.Argument, "value") &&
                    directive.Expression is SimpleExpressionNode { IsStatic: false })
                {
                    return false;
                }
            }
        }

        foreach (var child in node.Children)
        {
            nodeCount++;
            if (child is ElementNode childElement)
            {
                if (childElement.Properties.Count > 0)
                {
                    elementWithBindingCount++;
                }

                if (!WalkStringifiable(childElement, ref nodeCount, ref elementWithBindingCount))
                {
                    return false;
                }
            }
            else if (child is not (TextNode or CommentNode))
            {
                // Interpolations/compounds would need constant evaluation Vuecs does not do (AOT); such
                // content never reaches a cached element, but bail defensively rather than drop it.
                return false;
            }
        }

        return true;
    }

    private static void StringifyNode(StringBuilder builder, TemplateSyntaxNode node, TransformContext context)
    {
        switch (node)
        {
            case ElementNode element:
                StringifyElement(builder, element, context);
                break;
            case TextNode text:
                EscapeHtml(builder, text.Content);
                break;
            case CommentNode comment:
                builder.Append("<!--");
                EscapeHtml(builder, comment.Content);
                builder.Append("-->");
                break;
            default:
                // Unreachable: AnalyzeNode bails on any child that is not an element/text/comment.
                throw new InvalidOperationException(
                    $"Cannot stringify a '{node.NodeType}' node without constant evaluation.");
        }
    }

    private static void StringifyElement(StringBuilder builder, ElementNode node, TransformContext context)
    {
        builder.Append('<').Append(node.Tag);
        foreach (var property in node.Properties)
        {
            if (property is AttributeNode attribute)
            {
                builder.Append(' ').Append(attribute.Name);
                if (attribute.Value is not null)
                {
                    builder.Append("=\"");
                    EscapeHtml(builder, attribute.Value.Content);
                    builder.Append('"');
                }
            }

            // A DirectiveNode is unreachable on a cached, stringifiable element: AnalyzeNode bails on any
            // binding whose value is not a stringifiable constant, and Vuecs never classifies an opaque
            // v-bind/v-html/v-text expression as constant (no constant evaluation). See docs/DESIGN.md.
        }

        if (context.ScopeId is not null)
        {
            builder.Append(' ').Append(context.ScopeId);
        }

        builder.Append('>');
        foreach (var child in node.Children)
        {
            StringifyNode(builder, child, context);
        }

        if (!CompilerDomKnowledge.IsVoidTag(node.Tag))
        {
            builder.Append("</").Append(node.Tag).Append('>');
        }
    }

    // Port of @vue/shared escapeHtml (packages/shared/src/escapeHtml.ts): escapes " & ' < > so the
    // serialized string round-trips through innerHTML to the same DOM. Applied to text, comment, and
    // attribute-value content, matching upstream's stringifyNode/stringifyElement.
    private static void EscapeHtml(StringBuilder builder, string value)
    {
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("&quot;");
                    break;
                case '&':
                    builder.Append("&amp;");
                    break;
                case '\'':
                    builder.Append("&#39;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }
    }

    // Port of isStringifiableAttr: a known attribute for the element's namespace, or a data-/aria- attribute.
    // MathML has no known-attribute table in the Vuecs shared DOM knowledge, so it is conservatively
    // non-stringifiable (the element still caches). See docs/DESIGN.md.
    private static bool IsStringifiableAttribute(string name, ElementNamespace ns)
    {
        var known = ns switch
        {
            ElementNamespace.Html => CompilerDomKnowledge.IsKnownHtmlAttribute(name),
            ElementNamespace.Svg => CompilerDomKnowledge.IsKnownSvgAttribute(name),
            _ => false,
        };

        return known ||
               name.StartsWith("data-", StringComparison.Ordinal) ||
               name.StartsWith("aria-", StringComparison.Ordinal);
    }
}
