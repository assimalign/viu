using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The output of <see cref="Transformer.Transform(RootNode, TransformOptions)"/>: the root code-generation
/// node plus the finalized helper/component/directive/hoist/cache metadata.
/// </summary>
/// <remarks>
/// Because the parse AST is immutable, the transformed structure and its code-generation nodes are surfaced
/// here rather than mutated onto the input. <see cref="Children"/> is the transformed top-level tree (for
/// structural inspection); <see cref="CodegenNode"/> is the render-function IR that code generation
/// ([V01.01.05.05]) consumes, and <see cref="GetCodegenNode"/> resolves the code-generation node of any
/// element/container reachable from it.
/// </remarks>
public sealed record TransformResult
{
    private readonly TransformContext context;

    internal TransformResult(
        TransformContext context,
        TemplateSyntaxNode? codegenNode,
        SyntaxList<TemplateChildNode> children,
        IReadOnlyList<RuntimeHelper> helpers,
        IReadOnlyList<string> components,
        IReadOnlyList<string> directives,
        IReadOnlyList<TemplateSyntaxNode?> hoists,
        IReadOnlyList<CacheExpression?> cached)
    {
        this.context = context;
        CodegenNode = codegenNode;
        Children = children;
        Helpers = helpers;
        Components = components;
        Directives = directives;
        Hoists = hoists;
        Cached = cached;
    }

    /// <summary>The root render-function code-generation node, or <see langword="null"/> for an empty template.</summary>
    public TemplateSyntaxNode? CodegenNode { get; }

    /// <summary>The transformed top-level children (structural directives folded into <see cref="IfNode"/>/<see cref="ForNode"/>).</summary>
    public SyntaxList<TemplateChildNode> Children { get; }

    /// <summary>The runtime helpers the generated render function imports.</summary>
    public IReadOnlyList<RuntimeHelper> Helpers { get; }

    /// <summary>The user component names to resolve.</summary>
    public IReadOnlyList<string> Components { get; }

    /// <summary>The user directive names to resolve.</summary>
    public IReadOnlyList<string> Directives { get; }

    /// <summary>The hoisted constant slots (populated by [V01.01.05.07]).</summary>
    public IReadOnlyList<TemplateSyntaxNode?> Hoists { get; }

    /// <summary>The cache slots reserved by <c>v-once</c>, <c>v-memo</c>, and cached handlers.</summary>
    public IReadOnlyList<CacheExpression?> Cached { get; }

    /// <summary>
    /// Gets whether the transform omitted client-only operations for a server-markup target.
    /// </summary>
    /// <remarks>
    /// The server render emitter requires this mode so any virtual-node fallback remains free of
    /// browser-only event and directive dependencies. Specified by
    /// <c>[SSR-COMPILE-1]</c> and <c>[SSR-COMPILE-2]</c>.
    /// </remarks>
    public bool IsServerRendering => context.IsServerRendering;

    /// <summary>
    /// Gets the scoped-style attribute stamped on every directly rendered native element, or null.
    /// </summary>
    /// <remarks>Specified by <c>[V01.01.06.04]</c> and <c>[SSR-COMPILE-3]</c>.</remarks>
    public string? ScopeId => context.ScopeId;

    /// <summary>
    /// Resolves the code-generation node of <paramref name="node"/>: a container carries it directly, while an
    /// element's node is looked up from the transform's side table.
    /// </summary>
    /// <param name="node">An element or container reachable from the transformed tree.</param>
    public TemplateSyntaxNode? GetCodegenNode(TemplateSyntaxNode node) => node switch
    {
        IfNode ifNode => ifNode.CodegenNode,
        ForNode forNode => forNode.CodegenNode,
        TextCallNode textCall => textCall.CodegenNode,
        _ => context.GetCodegenNode(node),
    };

    /// <summary>
    /// Returns the transform's current child list for an element instead of its immutable parse-time
    /// snapshot. The server writer consumes this list so expression-prefix and structural replacements
    /// are identical to the virtual-node writer's inputs.
    /// </summary>
    /// <param name="element">The transformed element.</param>
    /// <returns>The element's frozen transformed children.</returns>
    internal IReadOnlyList<TemplateChildNode> GetTransformedChildren(ElementNode element)
    {
        if (!context.TryGetWorkingChildren(element, out List<TemplateSyntaxNode>? children))
        {
            return element.Children;
        }

        return TransformFreeze.FreezeChildren(children);
    }
}
