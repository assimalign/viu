using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The template language's <see cref="SyntaxParser{T}"/>: the registration-friendly instance adapter
/// over <see cref="TemplateParser"/> that build tooling plugs into the shared parser pipeline (e.g.
/// registered on an aggregate parser for a single-file component's <c>@template</c> block). Parsing
/// semantics are exactly <see cref="TemplateParser.Parse(string, ParserOptions)"/> — the upstream-pinned
/// <c>baseParse</c> port stays the authoritative entry point — with the recoverable
/// <see cref="ParserOptions.OnError"/> errors additionally surfaced as the result's
/// <see cref="SyntaxParserResult.Diagnostics"/> so registration-based consumers read them uniformly.
/// </summary>
public sealed class TemplateSyntaxParser : SyntaxParser<TemplateSyntaxNode>
{
    private readonly ParserOptions parserOptions;

    /// <summary>Creates the parser with default (base-mode) template options and no analyzers.</summary>
    public TemplateSyntaxParser()
        : this(new ParserOptions(), new SyntaxParserOptions<TemplateSyntaxNode>())
    {
    }

    /// <summary>Creates the parser with the given template <paramref name="parserOptions"/> and no analyzers.</summary>
    /// <param name="parserOptions">The upstream-pinned template parser options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parserOptions"/> is <see langword="null"/>.</exception>
    public TemplateSyntaxParser(ParserOptions parserOptions)
        : this(parserOptions, new SyntaxParserOptions<TemplateSyntaxNode>())
    {
    }

    /// <summary>Creates the parser with the given template and pipeline options.</summary>
    /// <param name="parserOptions">The upstream-pinned template parser options.</param>
    /// <param name="options">The shared pipeline options — analyzers and the analysis timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parserOptions"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public TemplateSyntaxParser(ParserOptions parserOptions, SyntaxParserOptions<TemplateSyntaxNode> options)
        : base(options)
    {
        this.parserOptions = parserOptions ?? throw new ArgumentNullException(nameof(parserOptions));
    }

    /// <summary>Parses bare <paramref name="text"/> into the template result.</summary>
    /// <param name="text">The template source.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The template result — the located AST root plus any recoverable diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public TemplateSyntaxParserResult ParseTemplate(string text, CancellationToken cancellationToken = default)
        => (TemplateSyntaxParserResult)ParseSyntax(text, cancellationToken);

    /// <summary>Parses <paramref name="source"/> into the template result.</summary>
    /// <param name="source">The template source.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The template result — the located AST root plus any recoverable diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public TemplateSyntaxParserResult ParseTemplate(SyntaxSource source, CancellationToken cancellationToken = default)
        => (TemplateSyntaxParserResult)ParseSyntax(source, cancellationToken);

    /// <inheritdoc />
    protected override SyntaxParserResult<TemplateSyntaxNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var errors = new List<Diagnostic>();

        // Intercept OnError on a per-parse shallow copy so the recoverable errors land on the result
        // without mutating the caller's options; a caller-supplied OnError still sees every error
        // (upstream's push model is preserved, the result channel is additive).
        var effectiveOptions = parserOptions.Clone();
        var callerOnError = effectiveOptions.OnError;
        effectiveOptions.OnError = error =>
        {
            errors.Add(error);
            callerOnError?.Invoke(error);
        };

        var root = TemplateParser.Parse(source.Text, effectiveOptions);
        var diagnostics = errors.Count == 0
            ? SyntaxList<Diagnostic>.Empty
            : new SyntaxList<Diagnostic>(errors.ToArray());

        return new TemplateSyntaxParserResult(root, diagnostics);
    }
}
