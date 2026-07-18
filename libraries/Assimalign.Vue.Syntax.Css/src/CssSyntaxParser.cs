using System.Threading;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The CSS language's <see cref="SyntaxParser{T}"/> — the parser build tooling registers for
/// <c>@style</c> blocks and <c>.css</c> sources (scoped CSS [V01.01.06.04], CSS Modules
/// [V01.01.06.06], and build-embedded style tooling such as utility-class generation).
/// </summary>
/// <remarks>
/// Scaffold: the parse currently produces a single located <see cref="CssStylesheetNode"/> carrying
/// the raw source, with no diagnostics — enough to pin the pipeline plumbing (registration dispatch,
/// value-equatable results) while tokenizer and rule-level parsing per CSS Syntax Module Level 3
/// (https://www.w3.org/TR/css-syntax-3/) land with the work items above.
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
        var stylesheet = new CssStylesheetNode
        {
            Content = source.Text,
            Location = WholeSourceLocation(source.Text),
        };

        return new SyntaxParserResult<CssSyntaxNode>(new SyntaxList<CssSyntaxNode>(new CssSyntaxNode[] { stylesheet }));
    }

    // Spans the whole source, upholding the SourceLocation exact-slice invariant. Replaced by real
    // tokenizer positions when rule-level parsing lands.
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
