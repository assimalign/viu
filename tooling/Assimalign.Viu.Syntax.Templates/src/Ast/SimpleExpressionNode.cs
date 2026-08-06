namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A leaf expression — either a static string (a static directive argument or modifier) or a raw
/// dynamic expression string. The parser does <em>not</em> parse expression bodies into a syntax tree:
/// at parse time an expression is opaque text plus its exact span, and only the transform stage
/// (<see cref="TransformOptions.PrefixIdentifiers"/>) classifies and rewrites identifiers. Keeping the
/// body opaque is what lets a parse stay a pure, cacheable function of the template source alone.
/// </summary>
public sealed record SimpleExpressionNode : ExpressionNode
{
    /// <summary>The expression text (for a dynamic argument, without the surrounding brackets).</summary>
    public required string Content { get; init; }

    /// <summary>Whether the content is a static string rather than a dynamic expression.</summary>
    public required bool IsStatic { get; init; }

    /// <summary>The static-ness level (see <see cref="ConstantType"/>).</summary>
    public ConstantType ConstantType { get; init; }

    /// <summary>
    /// Whether this expression is an event-handler key. Set by the
    /// <c>v-on</c> transform ([V01.01.05.03]) so prop normalization does not treat a dynamic handler key as a
    /// dynamic prop key. The parser never sets this.
    /// </summary>
    public bool IsHandlerKey { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.SimpleExpression;
}
