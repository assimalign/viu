using System.Collections.Generic;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// The language-agnostic result of a <see cref="SyntaxParser"/> parse: the produced nodes and any
/// recoverable diagnostics. Concrete results derive from <see cref="SyntaxParserResult{T}"/> to expose
/// the typed tree; consumers that dispatch parsers by registration (see
/// <see cref="AggregateSyntaxParser{T}"/>) read nodes and diagnostics through this base without
/// knowing the language.
/// </summary>
/// <remarks>
/// A record so results are value-equatable end to end — identical input yields an equal result, the
/// incremental-caching contract of the derived parsers ([V01.01.05.01]/[V01.01.06.01]). Diagnostics
/// ride in a <see cref="SyntaxList{T}"/> for the same reason: a reference-compared collection would
/// silently defeat the cache.
/// </remarks>
public abstract record SyntaxParserResult
{
    /// <summary>Creates the result carrying <paramref name="diagnostics"/>.</summary>
    /// <param name="diagnostics">The recoverable diagnostics, in report order.</param>
    protected SyntaxParserResult(SyntaxList<Diagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>The recoverable diagnostics, in report order; empty when the source is clean.</summary>
    public SyntaxList<Diagnostic> Diagnostics { get; init; }

    /// <summary>The produced nodes, untyped. Typed consumers use <see cref="SyntaxParserResult{T}.Nodes"/>.</summary>
    public IReadOnlyList<SyntaxNode> Nodes => GetNodes();

    /// <summary>Projects the typed node list for the untyped <see cref="Nodes"/> view.</summary>
    /// <returns>The produced nodes.</returns>
    protected abstract IReadOnlyList<SyntaxNode> GetNodes();
}
