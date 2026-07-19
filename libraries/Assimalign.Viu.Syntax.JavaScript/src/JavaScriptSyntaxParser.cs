using System.Threading;

namespace Assimalign.Viu.Syntax.JavaScript;

/// <summary>
/// The JavaScript language's <see cref="SyntaxParser{T}"/> — the parser build tooling registers for
/// <c>.js</c>/<c>.mjs</c> sources around the JS-interop boundary (interop glue modules, host-page
/// scripts). Component logic is C# (Roslyn's domain), and template expressions stay in
/// <c>Assimalign.Viu.Syntax.Templates</c>.
/// </summary>
/// <remarks>
/// Scaffold: the parse currently produces a single located <see cref="JavaScriptProgramNode"/>
/// carrying the raw source, with no diagnostics — enough to pin the pipeline plumbing (registration
/// dispatch, value-equatable results) while statement-level parsing per ECMA-262
/// (https://tc39.es/ecma262/) lands with its own work item.
/// </remarks>
public sealed class JavaScriptSyntaxParser : SyntaxParser<JavaScriptSyntaxNode>
{
    /// <summary>Creates the parser with default options (no analyzers).</summary>
    public JavaScriptSyntaxParser()
    {
    }

    /// <summary>Creates the parser with the given <paramref name="options"/>.</summary>
    /// <param name="options">The shared pipeline options — analyzers and the analysis timeout.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public JavaScriptSyntaxParser(SyntaxParserOptions<JavaScriptSyntaxNode> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override SyntaxParserResult<JavaScriptSyntaxNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var program = new JavaScriptProgramNode
        {
            Content = source.Text,
            Location = WholeSourceLocation(source.Text),
        };

        return new SyntaxParserResult<JavaScriptSyntaxNode>(new SyntaxList<JavaScriptSyntaxNode>(new JavaScriptSyntaxNode[] { program }));
    }

    // Spans the whole source, upholding the SourceLocation exact-slice invariant. Replaced by real
    // tokenizer positions when statement-level parsing lands.
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
