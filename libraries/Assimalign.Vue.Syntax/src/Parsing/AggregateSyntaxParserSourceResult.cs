namespace Assimalign.Vue.Syntax;

/// <summary>
/// One dispatched parse inside an <see cref="AggregateSyntaxParserResult{T}"/>: the container node
/// the embedded source came from, the <see cref="SyntaxSource"/> the registration matched, and the
/// registered parser's result. Value-equatable end to end, preserving the incremental-caching
/// contract through the aggregate layer.
/// </summary>
/// <param name="Node">The container node the embedded source came from.</param>
/// <param name="Source">The embedded source the registration matched.</param>
/// <param name="Result">The registered parser's result for <paramref name="Source"/>.</param>
/// <typeparam name="T">The container's node type.</typeparam>
public sealed record AggregateSyntaxParserSourceResult<T>(T Node, SyntaxSource Source, SyntaxParserResult Result) where T : SyntaxNode;
