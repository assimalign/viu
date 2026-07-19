namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A pseudo-class or pseudo-element selector. For an ordinary pseudo (<see cref="CssPseudoSelectorKind.Normal"/>)
/// the serializer emits <see cref="SyntaxNode.Location"/><c>.Source</c> verbatim, so <c>:hover</c>,
/// <c>::before</c>, and <c>:not(.x)</c> round-trip unchanged. For Vue's reserved functional pseudos
/// (<c>:deep()</c>, <c>:slotted()</c>, <c>:global()</c>) the parsed <see cref="Argument"/> carries the
/// inner selector list the scoped rewrite consumes.
/// </summary>
public sealed record CssPseudoSelectorNode : CssSelectorPartNode
{
    /// <summary>The pseudo's role in the scoped rewrite.</summary>
    public required CssPseudoSelectorKind Pseudo { get; init; }

    /// <summary>The pseudo name without its leading colon(s), as authored (e.g. <c>hover</c>, <c>before</c>, <c>deep</c>, <c>v-deep</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Whether the pseudo was written in double-colon (pseudo-element) form (<c>::before</c>).</summary>
    public required bool IsElement { get; init; }

    /// <summary>
    /// The parsed inner selector list for Vue's reserved functional pseudos (<c>:deep()</c>,
    /// <c>:slotted()</c>, <c>:global()</c>), or <see langword="null"/> for an ordinary pseudo (whose raw
    /// text is emitted verbatim).
    /// </summary>
    public CssSelectorListNode? Argument { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.PseudoSelector;
}
