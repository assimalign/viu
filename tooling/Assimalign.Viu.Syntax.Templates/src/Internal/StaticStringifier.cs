using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The DOM static-stringification pass (specified by <c>[SFC-OPT-2]</c>–<c>[SFC-OPT-4]</c>).
/// It runs at the end of the static-caching walk over a node's children:
/// contiguous runs of cached, stringifiable static siblings that reach the thresholds
/// (<see cref="NodeCountThreshold"/> nodes, or <see cref="ElementWithBindingCountThreshold"/> elements with
    /// attribute bindings) collapse into a single
    /// <c>createStaticVNode(markup, nodeCount, usesExtensibleMarkupLanguage)</c> call carrying the serialized
    /// fragment and its parsing mode. The runtime inserts it via one <c>innerHTML</c> assignment instead of
    /// creating each virtual node across the JS-interop boundary. This is the founding-decision-#4 interop
    /// reducer: every avoided node is a marshaling round-trip saved on WASM.
/// </summary>
/// <remarks>
/// <para>
/// Deliberate scope decisions, all AOT- and immutability-driven and pinned by tests
/// (see <c>docs/DESIGN.md</c>):
/// </para>
/// <list type="bullet">
/// <item>No constant-expression evaluation. Evaluating a constant interpolation at compile time would
/// require generating and running code, which the no-dynamic-codegen rule forbids outright. It also
/// never arises: no interpolation or dynamic binding is classified constant, so a
/// stringifiable cached subtree only ever contains static text, comments, and static attributes.
/// <see cref="AnalyzeNode"/> bails on anything else.</item>
/// <item>Only runs on a template root's children and a plain element's children (the two containers with
/// clean immutable write-back). Static descendants inside <c>v-if</c>/<c>v-for</c> bodies are still cached,
/// just not stringified.</item>
/// <item>Merged cache slots stay reserved-but-unused rather than being compacted and re-indexed, which
/// keeps <see cref="CacheExpression"/> immutable. The cost is a slightly larger cache array; the benefit
/// is that no already-emitted slot index is ever invalidated.</item>
/// </list>
/// </remarks>
internal static class StaticStringifier
{
    // The stringification thresholds are specified values ([SFC-OPT-2]) and are pinned by tests; below
    // them the raw-HTML insert costs more than the nodes it replaces.
    private const int NodeCountThreshold = 20;
    private const int ElementWithBindingCountThreshold = 5;

    // Table-section tags that innerHTML would reparent, so they are never stringified ([SFC-OPT-3]).
    private static readonly HashSet<string> NonStringifiableTags = new(StringComparer.Ordinal)
    {
        "caption", "thead", "tr", "th", "tbody", "td", "tfoot", "colgroup", "col",
    };

    /// <summary>
    /// Stringifies eligible contiguous cached-static runs among <paramref name="node"/>'s children.
    /// Bails for slot content, and
    /// only rewrites a <see cref="RootNode"/>'s working children or a plain element's
    /// <see cref="VirtualNodeCall"/> children.
    /// </summary>
    /// <param name="node">The container whose children were just cached.</param>
    /// <param name="context">The transform context.</param>
    public static void Run(TemplateSyntaxNode node, TransformContext context)
    {
        // Bail stringification for slot content: a slot's children are rendered by the parent into an
        // unknown position, so a raw-HTML insert cannot be placed safely.
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
                 context.GetCodegenNode(element) is VirtualNodeCall { Children: SyntaxList<TemplateChildNode> frozenChildren } vnode)
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
        bool? currentUsesExtensibleMarkupLanguage = null;

        var index = 0;
        for (; index < children.Count; index++)
        {
            if (GetCachedElement(children[index], context) is { } element &&
                AnalyzeNode(element) is { } analyzed)
            {
                var usesExtensibleMarkupLanguage =
                    element.Namespace is ElementNamespace.Svg or ElementNamespace.MathMl;
                if (currentUsesExtensibleMarkupLanguage is { } currentFormat &&
                    currentFormat != usesExtensibleMarkupLanguage)
                {
                    // A foreign-content integration point can have HTML children even though its parent is
                    // SVG or MathML. Keep each inserted fragment in one parsing mode; deriving the mode from
                    // the serialized children also handles a run that crosses back into foreign content.
                    index -= StringifyCurrentChunk(
                        children,
                        index,
                        nodeCount,
                        elementWithBindingCount,
                        currentChunk,
                        context,
                        currentFormat);
                    nodeCount = 0;
                    elementWithBindingCount = 0;
                    currentChunk.Clear();
                }

                currentUsesExtensibleMarkupLanguage = usesExtensibleMarkupLanguage;
                nodeCount += analyzed.NodeCount;
                elementWithBindingCount += analyzed.ElementWithBindingCount;
                currentChunk.Add(element);
                continue;
            }

            index -= StringifyCurrentChunk(
                children,
                index,
                nodeCount,
                elementWithBindingCount,
                currentChunk,
                context,
                currentUsesExtensibleMarkupLanguage ?? false);
            nodeCount = 0;
            elementWithBindingCount = 0;
            currentChunk.Clear();
            currentUsesExtensibleMarkupLanguage = null;
        }

        StringifyCurrentChunk(
            children,
            index,
            nodeCount,
            elementWithBindingCount,
            currentChunk,
            context,
            currentUsesExtensibleMarkupLanguage ?? false);
    }

    private static int StringifyCurrentChunk(
        List<TemplateSyntaxNode> children,
        int currentIndex,
        int nodeCount,
        int elementWithBindingCount,
        List<ElementNode> currentChunk,
        TransformContext context,
        bool usesExtensibleMarkupLanguage)
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

        // createStaticVNode(markup, nodeCount, usesExtensibleMarkupLanguage): the markup rides as a static
        // simple expression so the emitter writes it as a C#-escaped string literal; the count and format
        // marker ride as raw literals.
        var staticCall = Ir.CallExpression(
            context.Helper(HelperNames.CreateStatic),
            new object[]
            {
                Ir.SimpleExpression(html.ToString(), isStatic: true),
                currentChunk.Count.ToString(CultureInfo.InvariantCulture),
                Ir.SimpleExpression(usesExtensibleMarkupLanguage ? "true" : "false", isStatic: false),
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

            // The merged slots stay reserved-but-unused rather than being compacted and re-indexed, which
            // keeps CacheExpression immutable and every already-emitted slot index valid (docs/DESIGN.md).
        }

        return deleteCount;
    }

    private static ElementNode? GetCachedElement(TemplateSyntaxNode node, TransformContext context)
        => node is ElementNode { ElementType: ElementType.Element } element &&
           context.GetCodegenNode(element) is CacheExpression
            ? element
            : null;

    /// <summary>
    /// Analyzes a cached element: returns the node count and the count of
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

                // Bail on a compound or non-stringifiable-constant expression. In Viu's opaque model an
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
                // Interpolations/compounds would need constant evaluation Viu does not do (AOT); such
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
            // binding whose value is not a stringifiable constant, and Viu never classifies an opaque
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

    // Escapes " & ' < > so the serialized string round-trips through innerHTML to the same DOM
    // ([SFC-OPT-4]). Applied to text, comment, and attribute-value content alike.
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

    // A known attribute for the element's namespace, or a data-/aria- attribute. MathML uses a bounded
    // MathML Core table from the linked DOM knowledge seam; unknown and dynamic names bail out safely.
    private static bool IsStringifiableAttribute(string name, ElementNamespace ns)
    {
        var known = ns switch
        {
            ElementNamespace.Html => CompilerDomKnowledge.IsKnownHtmlAttribute(name),
            ElementNamespace.Svg => CompilerDomKnowledge.IsKnownSvgAttribute(name),
            ElementNamespace.MathMl => CompilerDomKnowledge.IsKnownMathMlAttribute(name),
            _ => false,
        };

        return known ||
               name.StartsWith("data-", StringComparison.Ordinal) ||
               name.StartsWith("aria-", StringComparison.Ordinal);
    }
}
