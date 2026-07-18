namespace Assimalign.Vue.Compiler;

/// <summary>
/// A code-generation call expression, e.g. <c>renderList(list, ...)</c> or <c>resolveDynamicComponent(is)</c>.
/// The C# port of Vue 3.5's <c>CallExpression</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>), part of the
/// intentionally minimal JavaScript AST subset the compiler emits for render-function generation.
/// </summary>
public sealed record CallExpression : SyntaxNode
{
    /// <summary>
    /// The callee: either a literal identifier <see cref="string"/> or a <see cref="RuntimeHelper"/> to be
    /// imported from the runtime (upstream's <c>string | symbol</c>).
    /// </summary>
    public required object Callee { get; init; }

    /// <summary>
    /// The call arguments. Each element is a literal <see cref="string"/>, a <see cref="RuntimeHelper"/>, or a
    /// <see cref="SyntaxNode"/> (a template child or another JS node), mirroring upstream's untyped argument
    /// array.
    /// </summary>
    public required SyntaxList<object> Arguments { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsCallExpression;
}
