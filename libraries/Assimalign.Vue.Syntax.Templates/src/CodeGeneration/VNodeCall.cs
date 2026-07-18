using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The central code-generation node describing one <c>createVNode</c>/<c>createElementBlock</c> call — the
/// vnode a template element, component, or fragment compiles to. The C# port of Vue 3.5's <c>VNodeCall</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
/// <remarks>
/// The <see cref="PatchFlag"/> and <see cref="DynamicProps"/> fields are populated by prop analysis
/// ([V01.01.05.03]) and the element-level patch-flag inference and block-emission decisions ([V01.01.05.06]);
/// <see cref="IsBlock"/> and <see cref="DisableTracking"/> record whether this vnode opens an optimization
/// block and whether its block tracking is suppressed. This node deliberately carries every field those
/// stages fill so downstream passes never reshape it.
/// </remarks>
public sealed record VNodeCall : TemplateSyntaxNode
{
    /// <summary>
    /// The vnode type: an element tag <see cref="string"/> (already quoted), a <see cref="RuntimeHelper"/>
    /// (e.g. <c>Fragment</c>/<c>Teleport</c>), or a <see cref="CallExpression"/>
    /// (<c>resolveDynamicComponent(...)</c>).
    /// </summary>
    public required object Tag { get; init; }

    /// <summary>The props expression (<see cref="ObjectExpression"/>/<see cref="CallExpression"/>/expression), or <see langword="null"/>.</summary>
    public TemplateSyntaxNode? Props { get; init; }

    /// <summary>
    /// The children: a <see cref="SyntaxList{T}"/> of <see cref="TemplateChildNode"/>, a single text child, a
    /// slots object, a <c>renderList</c> call for <c>v-for</c>, or <see langword="null"/> (upstream's union).
    /// </summary>
    public object? Children { get; init; }

    /// <summary>The patch-flag optimization hint, or <see langword="null"/> when none applies.</summary>
    public PatchFlags? PatchFlag { get; init; }

    /// <summary>
    /// The dynamic prop names list: a stringified array literal or a <see cref="SimpleExpressionNode"/>, or
    /// <see langword="null"/> (upstream's <c>string | SimpleExpressionNode | undefined</c>).
    /// </summary>
    public object? DynamicProps { get; init; }

    /// <summary>The runtime-directive arguments array (<c>withDirectives</c>), or <see langword="null"/>.</summary>
    public ArrayExpression? Directives { get; init; }

    /// <summary>Whether this vnode opens an optimization block.</summary>
    public bool IsBlock { get; init; }

    /// <summary>Whether block child tracking is disabled (a <c>v-for</c> fragment with a dynamic source).</summary>
    public bool DisableTracking { get; init; }

    /// <summary>Whether the vnode is a component (selects the <c>createVNode</c> vs <c>createElementVNode</c> helper).</summary>
    public bool IsComponent { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.VNodeCall;
}
