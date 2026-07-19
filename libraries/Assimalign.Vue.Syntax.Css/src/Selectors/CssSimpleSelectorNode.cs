namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A simple selector — a type, universal, class, id, or attribute selector. Its
/// <see cref="SyntaxNode.Location"/><c>.Source</c> is the exact authored text (e.g. <c>.foo</c>,
/// <c>#bar</c>, <c>div</c>, <c>[type="text"]</c>), which the serializer emits verbatim. A simple selector
/// is a candidate for the scoped rewrite's attribute-insertion point: the attribute is placed after the
/// last simple selector of the last compound in a complex selector.
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
