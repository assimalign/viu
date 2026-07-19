using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax;

/// <summary>
/// Configures a <see cref="SyntaxParser{T}"/>: the <see cref="SyntaxAnalyzer{T}"/> instances that run
/// over the parsed nodes and the time budget for that analysis pass. Language-specific parser options
/// stay on the concrete parser (e.g. the template parser's upstream-pinned <c>ParserOptions</c>) —
/// this type carries only the shared pipeline configuration.
/// </summary>
/// <typeparam name="T">The root node type of the syntax tree the configured parser produces.</typeparam>
public class SyntaxParserOptions<T> where T : SyntaxNode
{
    /// <summary>The analyzers to run over the parsed nodes, in registration order.</summary>
    public List<SyntaxAnalyzer<T>> Analyzers { get; } = new List<SyntaxAnalyzer<T>>();

    /// <summary>
    /// The time budget for the whole analysis pass. When exceeded, the parse throws
    /// <see cref="OperationCanceledException"/>. Defaults to 5 seconds.
    /// </summary>
    public TimeSpan AnalyzerTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
