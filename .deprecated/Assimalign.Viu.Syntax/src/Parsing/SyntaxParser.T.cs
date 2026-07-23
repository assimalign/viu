using System;
using System.Threading;

namespace Assimalign.Viu.Syntax;

/// <summary>
/// The typed parser base: binds a parser to the root node type <typeparamref name="T"/> of the syntax
/// tree it produces (the template parser's <c>TemplateSyntaxNode</c>, the single-file-component
/// parser's <c>SingleFileComponentBlock</c>, a stylesheet's <c>CssSyntaxNode</c>, …) and runs the
/// shared parse pipeline: <see cref="ParseCore"/> produces the typed result, then the
/// <see cref="SyntaxAnalyzer{T}"/> instances registered on <see cref="SyntaxParserOptions{T}"/> run
/// over the nodes and append their diagnostics.
/// </summary>
/// <remarks>
/// Analyzers are synchronous with a cooperative <see cref="CancellationToken"/>, mirroring Roslyn's
/// analyzer model — the netstandard2.0 generator hosts this family runs in ([V01.01.05.05]/
/// [V01.01.06.02]) drive a synchronous pipeline, so an async analyzer surface would only add
/// scheduling cost. <see cref="SyntaxParserOptions{T}.AnalyzerTimeout"/> bounds the whole analysis
/// pass; exceeding it (or cancelling the caller's token) throws
/// <see cref="OperationCanceledException"/>.
/// </remarks>
/// <typeparam name="T">The root node type of the syntax tree this parser produces.</typeparam>
public abstract class SyntaxParser<T> : SyntaxParser where T : SyntaxNode
{
    private readonly SyntaxParserOptions<T> options;

    /// <summary>Creates the parser with default options (no analyzers).</summary>
    protected SyntaxParser()
        : this(new SyntaxParserOptions<T>())
    {
    }

    /// <summary>Creates the parser with the given <paramref name="options"/>.</summary>
    /// <param name="options">The options — registered analyzers and the analysis timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    protected SyntaxParser(SyntaxParserOptions<T> options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public sealed override SyntaxParserResult Parse(SyntaxSource source, CancellationToken cancellationToken = default)
        => ParseSyntax(source, cancellationToken);

    /// <summary>Parses bare <paramref name="text"/> into the typed result.</summary>
    /// <param name="text">The source text to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The typed parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public SyntaxParserResult<T> ParseSyntax(string text, CancellationToken cancellationToken = default)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return ParseSyntax(new SyntaxSource { Text = text }, cancellationToken);
    }

    /// <summary>Parses <paramref name="source"/> into the typed result.</summary>
    /// <param name="source">The source to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The typed parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public virtual SyntaxParserResult<T> ParseSyntax(SyntaxSource source, CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var result = ParseCore(source, cancellationToken);

        return Analyze(result, cancellationToken);
    }

    /// <summary>
    /// Produces the typed parse result for <paramref name="source"/>: the tree's nodes plus the
    /// diagnostics the parse itself reported. Analyzer diagnostics are appended afterwards by the base
    /// pipeline via a <c>with</c> clone, so implementations returning a derived result record keep
    /// their runtime type (and any extra members) intact.
    /// </summary>
    /// <param name="source">The source to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The typed parse result.</returns>
    protected abstract SyntaxParserResult<T> ParseCore(SyntaxSource source, CancellationToken cancellationToken);

    private SyntaxParserResult<T> Analyze(SyntaxParserResult<T> result, CancellationToken cancellationToken)
    {
        if (options.Analyzers.Count == 0)
        {
            return result;
        }

        using var timeoutSource = new CancellationTokenSource(options.AnalyzerTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var context = new SyntaxAnalyzerContext<T>(result.Nodes, linkedSource.Token);

        foreach (var analyzer in options.Analyzers)
        {
            linkedSource.Token.ThrowIfCancellationRequested();
            analyzer.Analyze(context);
        }

        if (context.Diagnostics.Count == 0)
        {
            return result;
        }

        var combined = new Diagnostic[result.Diagnostics.Count + context.Diagnostics.Count];
        for (var index = 0; index < result.Diagnostics.Count; index++)
        {
            combined[index] = result.Diagnostics[index];
        }

        for (var index = 0; index < context.Diagnostics.Count; index++)
        {
            combined[result.Diagnostics.Count + index] = context.Diagnostics[index];
        }

        return result with { Diagnostics = new SyntaxList<Diagnostic>(combined) };
    }
}
