namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation block statement — a sequence of statements forming a function body. It is produced
/// only for the memoized <c>v-for</c> loop body (<c>v-memo</c> combined with <c>v-for</c>), the one place
/// the emitter needs statements rather than a single expression.
/// </summary>
public sealed record BlockStatement : TemplateSyntaxNode
{
    /// <summary>
    /// The ordered statements. Each is a literal <see cref="string"/> or a <see cref="TemplateSyntaxNode"/>,
    /// because a transform assembles the body from fragments it cannot type uniformly.
    /// </summary>
    public required SyntaxList<object> Body { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.BlockStatement;
}
