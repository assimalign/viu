using System.Collections.Generic;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// The typed result of a <see cref="SyntaxParser{T}"/> parse: the tree's nodes as a structurally
/// equatable <see cref="SyntaxList{T}"/> plus the diagnostics. Language libraries derive sealed
/// result records from this to add their own accessors (the template result's root node, the
/// single-file-component result's descriptor) — the base pipeline's <c>with</c>-based diagnostic
/// appending preserves the derived runtime type.
/// </summary>
/// <typeparam name="T">The root node type of the produced syntax tree.</typeparam>
public record SyntaxParserResult<T> : SyntaxParserResult where T : SyntaxNode
{
    /// <summary>Creates a diagnostic-free result over <paramref name="nodes"/>.</summary>
    /// <param name="nodes">The produced nodes.</param>
    public SyntaxParserResult(SyntaxList<T> nodes)
        : this(nodes, SyntaxList<Diagnostic>.Empty)
    {
    }

    /// <summary>Creates the result over <paramref name="nodes"/> with <paramref name="diagnostics"/>.</summary>
    /// <param name="nodes">The produced nodes.</param>
    /// <param name="diagnostics">The recoverable diagnostics, in report order.</param>
    public SyntaxParserResult(SyntaxList<T> nodes, SyntaxList<Diagnostic> diagnostics)
        : base(diagnostics)
    {
        Nodes = nodes;
    }

    /// <summary>The produced nodes, typed.</summary>
    public new SyntaxList<T> Nodes { get; init; }

    /// <inheritdoc />
    protected sealed override IReadOnlyList<SyntaxNode> GetNodes() => Nodes;
}
