namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// One simple selector with its classification and authored text. Module class renaming updates
/// this text, and serializers consume the updated value.
/// </summary>
public sealed record CssSimpleSelectorNode : CssSelectorPartNode
{
    /// <summary>The simple-selector kind.</summary>
    public required CssSimpleSelectorKind Selector { get; init; }

    /// <summary>The exact authored selector text (e.g. <c>.foo</c>, <c>#bar</c>, <c>div</c>, <c>*</c>, <c>[type="text"]</c>).</summary>
    public required string Text { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.SimpleSelector;
}
