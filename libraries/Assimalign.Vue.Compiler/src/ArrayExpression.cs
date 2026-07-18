namespace Assimalign.Vue.Compiler;

/// <summary>
/// A code-generation array literal, e.g. the directive arguments array or the dynamic-slots entries. The C#
/// port of Vue 3.5's <c>ArrayExpression</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
public sealed record ArrayExpression : SyntaxNode
{
    /// <summary>
    /// The array elements. Each is a literal <see cref="string"/> or a <see cref="SyntaxNode"/>, mirroring
    /// upstream's <c>Array&lt;string | Node&gt;</c>.
    /// </summary>
    public required SyntaxList<object> Elements { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsArrayExpression;
}
