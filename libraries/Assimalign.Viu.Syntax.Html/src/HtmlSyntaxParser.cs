using System.Threading;

namespace Assimalign.Viu.Syntax.Html;

/// <summary>
/// The plain-HTML language's <see cref="SyntaxParser{T}"/> — the parser build tooling registers for
/// <c>.html</c> sources, above all the WASM host page (<c>wwwroot/index.html</c> entry rewriting, the
/// role Vite's HTML entry processing plays in a Vue build). Vue <em>template</em> markup is the
/// separate <c>Assimalign.Viu.Syntax.Templates</c> parser.
/// </summary>
/// <remarks>
/// Scaffold: the parse currently produces a single located <see cref="HtmlDocumentNode"/> carrying the
/// raw source, with no diagnostics — enough to pin the pipeline plumbing (registration dispatch,
/// value-equatable results) while element-level parsing per the WHATWG model
/// (https://html.spec.whatwg.org/multipage/parsing.html) lands with its own work item.
/// </remarks>
public sealed class HtmlSyntaxParser : SyntaxParser<HtmlSyntaxNode>
{
    /// <summary>Creates the parser with default options (no analyzers).</summary>
    public HtmlSyntaxParser()
    {
    }

    /// <summary>Creates the parser with the given <paramref name="options"/>.</summary>
    /// <param name="options">The shared pipeline options — analyzers and the analysis timeout.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public HtmlSyntaxParser(SyntaxParserOptions<HtmlSyntaxNode> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override SyntaxParserResult<HtmlSyntaxNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var document = new HtmlDocumentNode
        {
            Content = source.Text,
            Location = WholeSourceLocation(source.Text),
        };

        return new SyntaxParserResult<HtmlSyntaxNode>(new SyntaxList<HtmlSyntaxNode>(new HtmlSyntaxNode[] { document }));
    }

    // Spans the whole source, upholding the SourceLocation exact-slice invariant. Replaced by real
    // tokenizer positions when element-level parsing lands.
    private static SourceLocation WholeSourceLocation(string text)
    {
        var line = 1;
        var column = 1;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new SourceLocation(new Position(0, 1, 1), new Position(text.Length, line, column), text);
    }
}
