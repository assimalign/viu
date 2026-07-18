using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Vue.Syntax;

public abstract class SyntaxAnalyzer<T> where T: SyntaxNode
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task AnalyzeAsync(SyntaxAnalyzerContext<T> context, CancellationToken cancellationToken);
}
