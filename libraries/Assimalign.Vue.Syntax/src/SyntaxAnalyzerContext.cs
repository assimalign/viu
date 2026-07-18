using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Vue.Syntax.Diagnostics;

namespace Assimalign.Vue.Syntax;

public class SyntaxAnalyzerContext<T> where T : SyntaxNode
{
    public SyntaxAnalyzerContext(SyntaxList<T> nodes)
    {
        Nodes = nodes;
    }


    /// <summary>
    /// 
    /// </summary>
    public virtual SyntaxList<T> Nodes { get; }

    /// <summary>
    /// 
    /// </summary>
    public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
}
