using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The <c>.viu</c> single-file component's <see cref="AggregateSyntaxParser{T}"/>: the
/// registration-friendly instance adapter over <see cref="SingleFileComponentParser"/>. Block-level
/// slicing semantics are exactly <see cref="SingleFileComponentParser.Parse(string)"/> — the
/// <c>@vue/compiler-sfc</c>-parity static entry point stays authoritative and this parser never looks
/// inside a block's content <em>itself</em> — but each block is exposed to the aggregate registration
/// seam as a <see cref="SyntaxSource"/> (content, block name, <c>lang</c> option), so build tooling
/// can attach the template parser to <c>@template</c>, a stylesheet parser to <c>@style</c>, or a
/// custom tool's parser to a custom block, incremental-generator style, without this library
/// referencing any of them.
/// </summary>
public sealed class SingleFileComponentSyntaxParser : AggregateSyntaxParser<SingleFileComponentBlock>
{
    /// <summary>Creates the parser with default options (no analyzers, no registrations).</summary>
    public SingleFileComponentSyntaxParser()
    {
    }

    /// <summary>Creates the parser with the given <paramref name="options"/>.</summary>
    /// <param name="options">The options — analyzers, the analysis timeout, and the parser registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public SingleFileComponentSyntaxParser(AggregateSyntaxParserOptions<SingleFileComponentBlock> options)
        : base(options)
    {
    }

    /// <summary>Parses bare <paramref name="text"/> into the single-file-component result.</summary>
    /// <param name="text">The full <c>.viu</c> file text.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The result — the descriptor, the blocks in source order, diagnostics, and any dispatched block parses.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public SingleFileComponentSyntaxParserResult ParseComponent(string text, CancellationToken cancellationToken = default)
        => (SingleFileComponentSyntaxParserResult)ParseSyntax(text, cancellationToken);

    /// <summary>Parses <paramref name="source"/> into the single-file-component result.</summary>
    /// <param name="source">The full <c>.viu</c> file source.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The result — the descriptor, the blocks in source order, diagnostics, and any dispatched block parses.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public SingleFileComponentSyntaxParserResult ParseComponent(SyntaxSource source, CancellationToken cancellationToken = default)
        => (SingleFileComponentSyntaxParserResult)ParseSyntax(source, cancellationToken);

    /// <inheritdoc />
    protected override SyntaxParserResult<SingleFileComponentBlock> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var parse = SingleFileComponentParser.Parse(source.Text);
        var descriptor = parse.Descriptor;

        var blocks = new List<SingleFileComponentBlock>();
        if (descriptor.Template is not null)
        {
            blocks.Add(descriptor.Template);
        }

        if (descriptor.Script is not null)
        {
            blocks.Add(descriptor.Script);
        }

        foreach (var style in descriptor.Styles)
        {
            blocks.Add(style);
        }

        foreach (var customBlock in descriptor.CustomBlocks)
        {
            blocks.Add(customBlock);
        }

        // The descriptor groups blocks by kind; the node list restores source order, the contract of
        // SyntaxParserResult.Nodes (and the order dispatched results are reported in).
        blocks.Sort(static (left, right) => left.Location.Start.Offset.CompareTo(right.Location.Start.Offset));

        var diagnostics = SyntaxList<Diagnostic>.Empty;
        if (parse.Errors.Count > 0)
        {
            var errors = new Diagnostic[parse.Errors.Count];
            for (var index = 0; index < parse.Errors.Count; index++)
            {
                errors[index] = parse.Errors[index];
            }

            diagnostics = new SyntaxList<Diagnostic>(errors);
        }

        return new SingleFileComponentSyntaxParserResult(
            descriptor,
            new SyntaxList<SingleFileComponentBlock>(blocks.ToArray()),
            diagnostics);
    }

    /// <inheritdoc />
    protected override SyntaxSource? GetSyntaxSource(SingleFileComponentBlock node)
        => new SyntaxSource
        {
            Text = node.Content,
            Name = node.Name,
            Language = node.Lang,
        };
}
