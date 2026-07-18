using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// The context a <see cref="SyntaxAnalyzer{T}"/> runs against: the parsed nodes, the cancellation
/// token bounding the analysis pass, and the diagnostic sink. One context is shared by every analyzer
/// of a parse, so diagnostics accumulate in analyzer registration order.
/// </summary>
/// <typeparam name="T">The root node type of the analyzed syntax tree.</typeparam>
public sealed class SyntaxAnalyzerContext<T> where T : SyntaxNode
{
    private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();

    /// <summary>Creates the context over <paramref name="nodes"/>.</summary>
    /// <param name="nodes">The parsed nodes to analyze.</param>
    /// <param name="cancellationToken">The token bounding the analysis pass.</param>
    public SyntaxAnalyzerContext(SyntaxList<T> nodes, CancellationToken cancellationToken = default)
    {
        Nodes = nodes;
        CancellationToken = cancellationToken;
    }

    /// <summary>The parsed nodes to analyze.</summary>
    public SyntaxList<T> Nodes { get; }

    /// <summary>
    /// The token bounding the analysis pass (the caller's token linked with
    /// <see cref="SyntaxParserOptions{T}.AnalyzerTimeout"/>). Analyzers observe it in their traversal
    /// loops.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>The diagnostics reported so far, in report order.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => diagnostics;

    /// <summary>Reports <paramref name="diagnostic"/> onto the parse result.</summary>
    /// <param name="diagnostic">The diagnostic to report.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    public void ReportDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic is null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        diagnostics.Add(diagnostic);
    }
}
