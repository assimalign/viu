namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A pseudo-class or pseudo-element selector. Ordinary pseudos preserve their source text.
/// Recognized functional selector syntax carries an inner <see cref="Argument"/> list for
/// CSS Modules processing and lossless source representation; no scope attributes are introduced.
/// </summary>
public sealed record CssPseudoSelectorNode : CssSelectorPartNode
{
    /// <summary>The parsed pseudo-selector classification.</summary>
    public required CssPseudoSelectorKind Pseudo { get; init; }

    /// <summary>The pseudo name without its leading colon(s), as authored (e.g. <c>hover</c>, <c>before</c>, <c>deep</c>, <c>v-deep</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Whether the pseudo was written in double-colon (pseudo-element) form (<c>::before</c>).</summary>
    public required bool IsElement { get; init; }

    /// <summary>
    /// The parsed inner selector list for the reserved functional pseudos (<c>:deep()</c>,
    /// <c>:slotted()</c>, <c>:global()</c>), or <see langword="null"/> for an ordinary pseudo (whose raw
    /// text is emitted verbatim).
    /// </summary>
    public CssSelectorListNode? Argument { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.PseudoSelector;
}
