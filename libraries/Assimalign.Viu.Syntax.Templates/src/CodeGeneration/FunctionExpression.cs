namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation function expression — the slot function of a compiled slot, or the iterator of a
/// <c>renderList</c> call.
/// </summary>
public sealed record FunctionExpression : TemplateSyntaxNode
{
    /// <summary>
    /// The parameter list, or <see langword="null"/>. Each element is an <see cref="ExpressionNode"/> or a
    /// literal <see cref="string"/>, or a nested list for a destructured parameter.
    /// </summary>
    public SyntaxList<object> Parameters { get; init; }

    /// <summary>
    /// The function body's return value: a single <see cref="TemplateSyntaxNode"/> or a
    /// <see cref="SyntaxList{T}"/> of <see cref="TemplateChildNode"/>, or
    /// <see langword="null"/>.
    /// </summary>
    public object? Returns { get; init; }

    /// <summary>
    /// The function body as a <see cref="BlockStatement"/> when the function has explicit statements (the
    /// memoized <c>v-for</c> loop), or <see langword="null"/> when it simply returns <see cref="Returns"/>.
    /// </summary>
    public BlockStatement? Body { get; init; }

    /// <summary>Whether code generation should place the body on a new line.</summary>
    public bool Newline { get; init; }

    /// <summary>Whether this function is a slot function requiring the <c>withCtx</c> wrapper.</summary>
    public bool IsSlot { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsFunctionExpression;
}
