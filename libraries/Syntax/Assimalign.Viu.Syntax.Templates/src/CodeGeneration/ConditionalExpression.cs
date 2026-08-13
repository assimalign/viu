namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation ternary conditional, the shape a <c>v-if</c>/<c>v-else-if</c>/<c>v-else</c> chain
/// compiles to: each branch's condition selects its block, falling through to the next.
/// </summary>
public sealed record ConditionalExpression : TemplateSyntaxNode
{
    /// <summary>The condition being tested.</summary>
    public required TemplateSyntaxNode Test { get; init; }

    /// <summary>The value when <see cref="Test"/> is truthy (this branch's block).</summary>
    public required TemplateSyntaxNode Consequent { get; init; }

    /// <summary>
    /// The value when <see cref="Test"/> is falsy: the next branch's <see cref="ConditionalExpression"/>, a
    /// block, or a comment virtual-node call terminating the chain.
    /// </summary>
    public required TemplateSyntaxNode Alternate { get; init; }

    /// <summary>Whether code generation should break the alternate onto a new line.</summary>
    public bool Newline { get; init; } = true;

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.ConditionalExpression;
}
