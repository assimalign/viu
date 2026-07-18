namespace Assimalign.Vue.Syntax;

/// <summary>
/// The result of an <see cref="AggregateSyntaxParser{T}"/> parse: the container's own typed nodes and
/// diagnostics, plus one <see cref="AggregateSyntaxParserSourceResult{T}"/> per node whose embedded
/// source matched a registration. Container libraries derive sealed records from this to add their own
/// accessors (the single-file-component result's descriptor).
/// </summary>
/// <remarks>
/// Nodes whose embedded source matched no registration simply have no entry in
/// <see cref="SourceResults"/> — their raw content on the node itself remains the authoritative
/// representation, so an unregistered (or registration-free) aggregate parse degrades to the plain
/// container parse.
/// </remarks>
/// <typeparam name="T">The container's node type.</typeparam>
public record AggregateSyntaxParserResult<T> : SyntaxParserResult<T> where T : SyntaxNode
{
    /// <summary>Creates a diagnostic-free result over <paramref name="nodes"/>.</summary>
    /// <param name="nodes">The container's nodes.</param>
    public AggregateSyntaxParserResult(SyntaxList<T> nodes)
        : base(nodes)
    {
    }

    /// <summary>Creates the result over <paramref name="nodes"/> with <paramref name="diagnostics"/>.</summary>
    /// <param name="nodes">The container's nodes.</param>
    /// <param name="diagnostics">The container's recoverable diagnostics, in report order.</param>
    public AggregateSyntaxParserResult(SyntaxList<T> nodes, SyntaxList<Diagnostic> diagnostics)
        : base(nodes, diagnostics)
    {
    }

    /// <summary>
    /// The dispatched parses, in node order: one entry per node whose embedded source matched a
    /// registration. Empty when no registration matched (or none was configured).
    /// </summary>
    public SyntaxList<AggregateSyntaxParserSourceResult<T>> SourceResults { get; init; }
        = SyntaxList<AggregateSyntaxParserSourceResult<T>>.Empty;
}
