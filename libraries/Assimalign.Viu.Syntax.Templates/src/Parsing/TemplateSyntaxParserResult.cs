namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The <see cref="TemplateSyntaxParser"/> result: the located AST <see cref="Root"/> (the same
/// <see cref="RootNode"/> <see cref="TemplateParser.Parse(string, ParserOptions)"/> returns) plus the
/// recoverable <c>CompilerError</c> diagnostics the parse reported. The base
/// <see cref="Assimalign.Viu.Syntax.SyntaxParserResult{T}.Nodes"/> list holds exactly the root — the
/// tree hangs off it — so registration-based consumers walk the same AST typed consumers do.
/// </summary>
public sealed record TemplateSyntaxParserResult : SyntaxParserResult<TemplateSyntaxNode>
{
    /// <summary>Creates the result for <paramref name="root"/> with <paramref name="diagnostics"/>.</summary>
    /// <param name="root">The located AST root.</param>
    /// <param name="diagnostics">The recoverable parse errors, in report order.</param>
    public TemplateSyntaxParserResult(RootNode root, SyntaxList<Diagnostic> diagnostics)
        : base(new SyntaxList<TemplateSyntaxNode>(new TemplateSyntaxNode[] { root }), diagnostics)
    {
        Root = root;
    }

    /// <summary>The located AST root.</summary>
    public RootNode Root { get; }
}
