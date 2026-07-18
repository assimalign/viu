using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Vue.Syntax;

public abstract class SyntaxParser<T> : SyntaxParser where T : SyntaxNode
{
    private readonly SyntaxParserOptions<T> _options;

    protected SyntaxParser(SyntaxParserOptions<T> options)
    {
        _options = options;
    }

    public sealed override SyntaxParserResult Parse(string source)
    {
        return Parse(source);
    }

    protected abstract SyntaxList<T> ParseCore(string source);


    public virtual SyntaxParserResult<T> ParseSyntax(string query)
    {
        var nodes = ParseCore(query);
        var context = new SyntaxAnalyzerContext<T>(nodes);

        Analyze(context, _options.AnalyzerTimeout);

        return new SyntaxParserResult<T>(nodes, context.Diagnostics);
    }


    private void Analyze(SyntaxAnalyzerContext<T> context, TimeSpan timeout)
    {
        using var cancellationTokenSource = new CancellationTokenSource(timeout); // Max 10 seconds for analysis
#if !DEBUG
        cancellationTokenSource.Token.ThrowIfCancellationRequested();
#endif
        var analyzers = new List<Task>();

        foreach (var analyzer in _options.Analyzers)
        {
            analyzers.Add(analyzer.AnalyzeAsync(context, cancellationTokenSource.Token));
        }
        while (analyzers.Any())
        {
            var task = Task.WhenAny(analyzers);

            while (!task.IsCompleted)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationTokenSource.Token);
                }
            }

            analyzers.Remove(task.Result);
        }
    }
}
