namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The base of every template AST node: an immutable, value-comparable record carrying the node's
/// <see cref="SyntaxNode.Location"/> (inherited from the shared <see cref="SyntaxNode"/> base) plus the
/// template-specific <see cref="NodeType"/> discriminator. Records give the whole tree structural equality, which is
/// the incremental-caching contract of [V01.01.05.01]: parsing equal input twice yields equal ASTs.
/// </summary>
public abstract record TemplateSyntaxNode : SyntaxNode
{
    /// <summary>The node kind discriminator.</summary>
    public abstract NodeType NodeType { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)NodeType;
}
