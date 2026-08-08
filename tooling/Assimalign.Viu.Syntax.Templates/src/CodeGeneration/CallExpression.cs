namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation call expression, e.g. <c>renderList(list, ...)</c> or <c>resolveDynamicComponent(is)</c>.
/// Part of the intentionally minimal expression subset the compiler emits for render-function
/// generation — just enough to describe a call, never a general-purpose syntax tree.
/// </summary>
public sealed record CallExpression : TemplateSyntaxNode
{
    /// <summary>
    /// The callee: either a literal identifier <see cref="string"/> or a <see cref="RuntimeHelper"/> to be
    /// imported from the runtime; the helper form is what lets the emitter register the import.
    /// </summary>
    public required object Callee { get; init; }

    /// <summary>
    /// The call arguments. Each element is a literal <see cref="string"/>, a <see cref="RuntimeHelper"/>, or a
    /// <see cref="TemplateSyntaxNode"/> (a template child or another code-generation node) — a heterogeneous argument
    /// array.
    /// </summary>
    public required SyntaxList<object> Arguments { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.CallExpression;
}
