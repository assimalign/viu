namespace Assimalign.Viu.Syntax;

/// <summary>
/// A unit of parseable input: the source text plus the identifying metadata a build tool knows about
/// it. This is the value <see cref="SyntaxParser.Parse(SyntaxSource, System.Threading.CancellationToken)"/>
/// consumes and the value an <see cref="AggregateSyntaxParser{T}"/> registration's
/// <see cref="SyntaxSourcePredicate"/> matches on — the same role an additional-file/hint-name pair
/// plays when registering outputs in a Roslyn incremental generator: tooling selects a parser by file
/// type or content, not by hard-wired dependency.
/// </summary>
/// <remarks>
/// Immutable and value-equatable so results that embed it keep the incremental-caching contract of the
/// derived parsers ([V01.01.05.01]/[V01.01.06.01]).
/// </remarks>
public sealed record SyntaxSource
{
    /// <summary>The source text to parse.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The source's name when known, otherwise <see langword="null"/>: a file name or path for
    /// file-level sources (e.g. <c>App.viu</c>, <c>site.css</c>), or a block name for block-level
    /// sources sliced out of a single-file component (e.g. <c>template</c>, <c>style</c>).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The language hint when known, otherwise <see langword="null"/> — e.g. the value of a
    /// single-file-component block's <c>lang</c> option (<c>@style lang="scss"</c> yields
    /// <c>scss</c>).
    /// </summary>
    public string? Language { get; init; }
}
