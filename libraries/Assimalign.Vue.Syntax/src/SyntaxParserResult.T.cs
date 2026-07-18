using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

using Diagnostics;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class SyntaxParserResult<T> : SyntaxParserResult where T : SyntaxNode
{
    public SyntaxParserResult(SyntaxList<T> nodes) 
        : base(nodes)
    {
    }

    public SyntaxParserResult(SyntaxList<T> nodes, IReadOnlyList<Diagnostic> diagnostics) 
        : base(nodes, diagnostics)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual new SyntaxList<T> Nodes => (SyntaxList<T>)base.Nodes;
}
