using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Parses component style blocks and CSS sources into immutable, located CSS syntax nodes.
/// The aggregate parser dispatches embedded style content through this implementation.
/// CSS Modules and binding rewrites consume the same tree. Parsing recovers diagnostics
/// without throwing, except when cancellation is requested.
/// </summary>
/// <remarks>
/// Parsing is recoverable per the spec's error handling: malformed input reports a <see cref="CssError"/>
/// on the result's diagnostics and never throws (the only expected exception is
/// <see cref="System.OperationCanceledException"/>, on cancellation). The composition root registers this parser
/// against component style block sources on the aggregate seam.
/// </remarks>
public sealed class CssSyntaxParser : SyntaxParser<CssSyntaxNode>
{
    /// <summary>Creates the parser with default options (no analyzers).</summary>
    public CssSyntaxParser()
    {
    }

    /// <summary>Creates the parser with the given <paramref name="options"/>.</summary>
    /// <param name="options">The shared pipeline options — analyzers and the analysis timeout.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public CssSyntaxParser(SyntaxParserOptions<CssSyntaxNode> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override SyntaxParserResult<CssSyntaxNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var text = source.Text;
        var positions = new CssPositionMap(text);
        var diagnostics = new List<CssError>();

        var tokens = new CssTokenizer(text, positions, diagnostics).Tokenize();
        cancellationToken.ThrowIfCancellationRequested();

        var stylesheet = new CssParseEngine(text, positions, tokens, diagnostics).ParseStylesheet();

        var nodes = new SyntaxList<CssSyntaxNode>(new CssSyntaxNode[] { stylesheet });
        if (diagnostics.Count == 0)
        {
            return new SyntaxParserResult<CssSyntaxNode>(nodes);
        }

        var reported = new Diagnostic[diagnostics.Count];
        for (var index = 0; index < diagnostics.Count; index++)
        {
            reported[index] = diagnostics[index];
        }

        return new SyntaxParserResult<CssSyntaxNode>(nodes, new SyntaxList<Diagnostic>(reported));
    }
}
