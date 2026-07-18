using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// A typed parser over a <em>container</em> language — one whose nodes carry embedded sources written
/// in other languages, the single-file component being the canonical case (a <c>.viu</c> file's
/// <c>@template</c>/<c>@style</c>/custom blocks each hold a different language). The derived parser
/// slices the container into its typed nodes as usual via
/// <see cref="SyntaxParser{T}.ParseCore(SyntaxSource, CancellationToken)"/>; this base then maps each
/// node to its embedded <see cref="SyntaxSource"/> (via <see cref="GetSyntaxSource"/>) and dispatches
/// it to the first matching <see cref="SyntaxParser"/> registered on
/// <see cref="AggregateSyntaxParserOptions{T}"/> — the incremental-generator-style registration seam
/// that lets build tooling attach language parsers (a stylesheet parser for <c>@style</c>, a custom
/// tool's parser for a custom block) without the container library referencing them.
/// </summary>
/// <remarks>
/// With no registrations the aggregate parse is exactly the typed parse — embedded content stays raw,
/// preserving the container language's own contract (the single-file-component parser never looks
/// inside a block's content by itself, mirroring <c>@vue/compiler-sfc</c>'s <c>parse()</c>).
/// Implementations' <see cref="SyntaxParser{T}.ParseCore(SyntaxSource, CancellationToken)"/> MUST
/// return an <see cref="AggregateSyntaxParserResult{T}"/> (or a derived record) — the sealed pipeline
/// casts to it to attach the dispatched results.
/// </remarks>
/// <typeparam name="T">The container's node type — the unit an embedded source is attached to.</typeparam>
public abstract class AggregateSyntaxParser<T> : SyntaxParser<T> where T : SyntaxNode
{
    private readonly AggregateSyntaxParserOptions<T> options;

    /// <summary>Creates the parser with default options (no analyzers, no registrations).</summary>
    protected AggregateSyntaxParser()
        : this(new AggregateSyntaxParserOptions<T>())
    {
    }

    /// <summary>Creates the parser with the given <paramref name="options"/>.</summary>
    /// <param name="options">The options — analyzers, the analysis timeout, and the parser registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    protected AggregateSyntaxParser(AggregateSyntaxParserOptions<T> options)
        : base(options)
    {
        this.options = options;
    }

    /// <summary>
    /// Parses <paramref name="source"/> and dispatches each node's embedded source to its registered
    /// parser. The returned result is the <see cref="ParseAggregate(SyntaxSource, CancellationToken)"/>
    /// result; this override keeps every entry point — including the untyped
    /// <see cref="SyntaxParser.Parse(SyntaxSource, CancellationToken)"/> a build tool dispatches
    /// through — running the full aggregate pipeline.
    /// </summary>
    /// <param name="source">The source to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The aggregate result (statically typed as the plain result).</returns>
    public sealed override SyntaxParserResult<T> ParseSyntax(SyntaxSource source, CancellationToken cancellationToken = default)
    {
        var result = (AggregateSyntaxParserResult<T>)base.ParseSyntax(source, cancellationToken);

        if (options.Registrations.Count == 0)
        {
            return result;
        }

        var sourceResults = new List<AggregateSyntaxParserSourceResult<T>>();

        foreach (var node in result.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nodeSource = GetSyntaxSource(node);
            if (nodeSource is null)
            {
                continue;
            }

            foreach (var registration in options.Registrations)
            {
                if (!registration.Predicate(nodeSource))
                {
                    continue;
                }

                var nodeResult = registration.Parser.Parse(nodeSource, cancellationToken);
                sourceResults.Add(new AggregateSyntaxParserSourceResult<T>(node, nodeSource, nodeResult));
                break;
            }
        }

        if (sourceResults.Count == 0)
        {
            return result;
        }

        return result with
        {
            SourceResults = new SyntaxList<AggregateSyntaxParserSourceResult<T>>(sourceResults.ToArray()),
        };
    }

    /// <summary>Parses bare <paramref name="text"/> into the aggregate result.</summary>
    /// <param name="text">The source text to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The aggregate result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public AggregateSyntaxParserResult<T> ParseAggregate(string text, CancellationToken cancellationToken = default)
        => (AggregateSyntaxParserResult<T>)ParseSyntax(text, cancellationToken);

    /// <summary>Parses <paramref name="source"/> into the aggregate result.</summary>
    /// <param name="source">The source to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The aggregate result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public AggregateSyntaxParserResult<T> ParseAggregate(SyntaxSource source, CancellationToken cancellationToken = default)
        => (AggregateSyntaxParserResult<T>)ParseSyntax(source, cancellationToken);

    /// <summary>
    /// Maps a container node to the embedded source the registrations should match on, or
    /// <see langword="null"/> when the node carries none (dispatch skips it). The single-file-component
    /// parser, for example, maps a block to its raw content, block name, and <c>lang</c> option.
    /// </summary>
    /// <param name="node">The container node.</param>
    /// <returns>The embedded source, or <see langword="null"/>.</returns>
    protected abstract SyntaxSource? GetSyntaxSource(T node);
}
