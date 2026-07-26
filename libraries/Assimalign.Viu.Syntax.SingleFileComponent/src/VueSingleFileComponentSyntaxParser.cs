using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The registration-friendly aggregate parser for tag-based <c>.vue</c> single-file components.
/// Container slicing is delegated to <see cref="VueSingleFileComponentParser"/>; each preserved block
/// is then exposed as a <see cref="SyntaxSource"/> so the same registered template, stylesheet, and
/// custom-block parsers used by the canonical <c>.viu</c> pipeline can consume it.
/// </summary>
public sealed class VueSingleFileComponentSyntaxParser : AggregateSyntaxParser<SingleFileComponentBlock>
{
    /// <summary>Creates a parser with no analyzer or embedded-language registrations.</summary>
    public VueSingleFileComponentSyntaxParser()
    {
    }

    /// <summary>Creates a parser with the supplied aggregate <paramref name="options"/>.</summary>
    /// <param name="options">The analyzers, timeout, and embedded-language parser registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public VueSingleFileComponentSyntaxParser(AggregateSyntaxParserOptions<SingleFileComponentBlock> options)
        : base(options)
    {
    }

    /// <summary>Parses tag-based <c>.vue</c> text and dispatches each matching block source.</summary>
    /// <param name="text">The full <c>.vue</c> source text.</param>
    /// <param name="cancellationToken">Cancels parsing and registered-parser dispatch.</param>
    /// <returns>The Vue descriptor, source-ordered blocks, diagnostics, and dispatched block parses.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public VueSingleFileComponentSyntaxParserResult ParseComponent(
        string text,
        CancellationToken cancellationToken = default)
        => (VueSingleFileComponentSyntaxParserResult)ParseSyntax(text, cancellationToken);

    /// <summary>Parses a tag-based <c>.vue</c> source and dispatches each matching block source.</summary>
    /// <param name="source">The full <c>.vue</c> syntax source.</param>
    /// <param name="cancellationToken">Cancels parsing and registered-parser dispatch.</param>
    /// <returns>The Vue descriptor, source-ordered blocks, diagnostics, and dispatched block parses.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public VueSingleFileComponentSyntaxParserResult ParseComponent(
        SyntaxSource source,
        CancellationToken cancellationToken = default)
        => (VueSingleFileComponentSyntaxParserResult)ParseSyntax(source, cancellationToken);

    /// <inheritdoc />
    protected override SyntaxParserResult<SingleFileComponentBlock> ParseCore(
        SyntaxSource source,
        CancellationToken cancellationToken)
    {
        var parse = VueSingleFileComponentParser.Parse(source.Text);
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

        if (descriptor.ScriptSetup is not null)
        {
            blocks.Add(descriptor.ScriptSetup);
        }

        foreach (var style in descriptor.Styles)
        {
            blocks.Add(style);
        }

        foreach (var customBlock in descriptor.CustomBlocks)
        {
            blocks.Add(customBlock);
        }

        blocks.Sort(static (left, right) => left.Location.Start.Offset.CompareTo(right.Location.Start.Offset));

        var diagnostics = SyntaxList<Diagnostic>.Empty;
        if (parse.Errors.Count > 0)
        {
            var values = new Diagnostic[parse.Errors.Count];
            for (var index = 0; index < parse.Errors.Count; index++)
            {
                values[index] = parse.Errors[index];
            }

            diagnostics = new SyntaxList<Diagnostic>(values);
        }

        return new VueSingleFileComponentSyntaxParserResult(
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
