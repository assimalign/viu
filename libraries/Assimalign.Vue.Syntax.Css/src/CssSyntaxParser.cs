using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The CSS language's <see cref="SyntaxParser{T}"/> — the parser build tooling registers for
/// <c>@style</c> blocks and <c>.css</c> sources (scoped CSS [V01.01.06.04], CSS Modules
/// [V01.01.06.06], and build-embedded style tooling such as utility-class generation). It tokenizes and
/// rule-parses the source per CSS Syntax Module Level 3 (https://www.w3.org/TR/css-syntax-3/) into a
/// <see cref="CssStylesheetNode"/> tree — qualified rules with parsed selector lists, at-rules
/// (<c>@media</c>/<c>@supports</c> recursed, <c>@keyframes</c> bodies), and declarations.
/// </summary>
/// <remarks>
/// Parsing is recoverable per the spec's error handling: malformed input reports a <see cref="CssError"/>
/// on the result's diagnostics and never throws (the only expected exception is
/// <see cref="System.OperationCanceledException"/>, on cancellation). The scoped-selector rewrite that
/// consumes the parsed tree lives in <see cref="CssScopedRewriter"/>; the composition root registers this
/// parser against <c>@style</c> block sources on the aggregate seam.
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
