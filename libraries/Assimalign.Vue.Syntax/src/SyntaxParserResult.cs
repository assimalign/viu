using Assimalign.Vue.Syntax.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

public abstract class SyntaxParserResult
{
    protected SyntaxParserResult(IReadOnlyList<SyntaxNode> nodes)
    {
        Nodes = nodes;
    }

    protected SyntaxParserResult(IReadOnlyList<SyntaxNode> nodes, IReadOnlyList<Diagnostic> diagnostics) : this(nodes)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual IReadOnlyList<SyntaxNode> Nodes { get; }

    /// <summary>
    /// 
    /// </summary>
    public virtual IReadOnlyList<Diagnostic> Diagnostics { get; } = [];
}
