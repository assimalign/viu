namespace Assimalign.Vue.Syntax;

/// <summary>
/// A post-parse analysis pass over a typed syntax tree, registered on
/// <see cref="SyntaxParserOptions{T}.Analyzers"/> and run by the <see cref="SyntaxParser{T}"/>
/// pipeline after <c>ParseCore</c>. Analyzers report through
/// <see cref="SyntaxAnalyzerContext{T}.ReportDiagnostic"/>; their diagnostics are appended after the
/// parse's own, in analyzer registration order.
/// </summary>
/// <remarks>
/// Synchronous with a cooperative token, mirroring Roslyn's analyzer model: implementations observe
/// <see cref="SyntaxAnalyzerContext{T}.CancellationToken"/> in their traversal loops so the
/// <see cref="SyntaxParserOptions{T}.AnalyzerTimeout"/> bound holds.
/// </remarks>
/// <typeparam name="T">The root node type of the analyzed syntax tree.</typeparam>
public abstract class SyntaxAnalyzer<T> where T : SyntaxNode
{
    /// <summary>Analyzes the parsed nodes on <paramref name="context"/>, reporting diagnostics onto it.</summary>
    /// <param name="context">The analysis context — the nodes, the cancellation token, and the diagnostic sink.</param>
    public abstract void Analyze(SyntaxAnalyzerContext<T> context);
}
