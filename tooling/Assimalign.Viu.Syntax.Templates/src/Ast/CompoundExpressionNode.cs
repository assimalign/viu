namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// An expression assembled by concatenating parts — interleaved literal strings, helper references, and
/// child expression nodes. The parser never produces these; transforms introduce them
/// (e.g. a dynamic <c>v-on</c> event name wrapped in <c>toHandlerKey(...)</c>, or merged adjacent text).
/// </summary>
/// <remarks>
/// <see cref="Parts"/> is deliberately heterogeneous — a transform assembles an expression out of
/// fragments it cannot type uniformly. Each element is one of a
/// literal <see cref="string"/>, a <see cref="RuntimeHelper"/>, or a <see cref="TemplateSyntaxNode"/>
/// (<see cref="SimpleExpressionNode"/>, <see cref="InterpolationNode"/>, <see cref="TextNode"/>, or a
/// nested <see cref="CompoundExpressionNode"/>). Expression bodies remain opaque at this stage
/// ([V01.01.05.04] layers in identifier prefixing); a compound expression only records how the pieces
/// concatenate.
/// </remarks>
public sealed record CompoundExpressionNode : ExpressionNode
{
    /// <summary>The ordered concatenation parts (strings, <see cref="RuntimeHelper"/>s, or nodes).</summary>
    public required SyntaxList<object> Parts { get; init; }

    /// <summary>
    /// Whether this expression is an event-handler key. Set by the
    /// <c>v-on</c> transform ([V01.01.05.03]) for dynamic event names so prop normalization does not treat the
    /// handler key as a dynamic prop key.
    /// </summary>
    public bool IsHandlerKey { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.CompoundExpression;
}
