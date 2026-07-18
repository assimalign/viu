namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// A <c>v-for</c> loop: the iterated source, the decomposed aliases, and the repeated children, which
/// compile to a fragment block rendered via <c>renderList</c>. The C# port of Vue 3.5's <c>ForNode</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>). See https://vuejs.org/guide/essentials/list.html.
/// </summary>
/// <remarks>
/// The fragment's keyed/unkeyed classification (carried on <see cref="CodegenNode"/>'s patch flag) feeds
/// patch-flag inference in [V01.01.05.06]. Alias decomposition (value/key/index, including tuple patterns) is
/// captured in <see cref="ParseResult"/>.
/// </remarks>
public sealed record ForNode : TemplateChildNode
{
    /// <summary>The iterated source expression.</summary>
    public required ExpressionNode Source { get; init; }

    /// <summary>The value alias, or <see langword="null"/>.</summary>
    public ExpressionNode? ValueAlias { get; init; }

    /// <summary>The key alias, or <see langword="null"/>.</summary>
    public ExpressionNode? KeyAlias { get; init; }

    /// <summary>The object index alias, or <see langword="null"/>.</summary>
    public ExpressionNode? ObjectIndexAlias { get; init; }

    /// <summary>The decomposed <c>v-for</c> expression pieces.</summary>
    public required ForParseResult ParseResult { get; init; }

    /// <summary>The repeated children (the <c>&lt;template v-for&gt;</c> contents, or the single element).</summary>
    public required SyntaxList<TemplateChildNode> Children { get; init; }

    /// <summary>The compiled fragment-block vnode call, or <see langword="null"/> before code generation.</summary>
    public TemplateSyntaxNode? CodegenNode { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.For;
}
