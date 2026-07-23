namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// An expression assembled by concatenating parts — interleaved literal strings, helper references, and
/// child expression nodes. The C# port of Vue 3.5's <c>CompoundExpressionNode</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>). The parser never produces these; transforms introduce them
/// (e.g. a dynamic <c>v-on</c> event name wrapped in <c>toHandlerKey(...)</c>, or merged adjacent text).
/// </summary>
/// <remarks>
/// <see cref="Parts"/> models upstream's heterogeneous <c>children</c> array. Each element is one of a
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
    /// Whether this expression is an event-handler key (upstream's <c>isHandlerKey</c>). Set by the
    /// <c>v-on</c> transform ([V01.01.05.03]) for dynamic event names so prop normalization does not treat the
    /// handler key as a dynamic prop key.
    /// </summary>
    public bool IsHandlerKey { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.CompoundExpression;
}
