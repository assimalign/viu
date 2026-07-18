using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

public class SyntaxParserOptions<T> where T : SyntaxNode
{
    /// <summary>
    /// 
    /// </summary>
    public List<SyntaxAnalyzer<T>> Analyzers { get; } = new List<SyntaxAnalyzer<T>>();

    /// <summary>
    /// Specify the timeout for query analysis. Default is 5 seconds.
    /// </summary>
    public TimeSpan AnalyzerTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
