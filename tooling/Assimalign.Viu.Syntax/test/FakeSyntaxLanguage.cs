using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Syntax;

// A minimal fake language exercising the shared pipeline, shared by SyntaxParserTests and
// AggregateSyntaxParserTests. Flat form: segments separated by ';' become nodes and a '!' segment
// reports a parse diagnostic. Container form: '|'-separated "name:content" blocks, with the "opaque"
// name carrying no embedded source.

internal sealed record FakeSyntaxNode : SyntaxNode
{
    public required string Content { get; init; }

    public override int RawKind => 0;
}

internal sealed record FakeDiagnostic : Diagnostic
{
    public override int RawCode => 1;
}

// Carries an extra member plus a distinguishing tag so tests can pin that the base pipeline's
// with-clone (diagnostic appending) preserves the derived result's runtime type and state.
internal sealed record FakeSyntaxParserResult : SyntaxParserResult<FakeSyntaxNode>
{
    public FakeSyntaxParserResult(SyntaxList<FakeSyntaxNode> nodes, SyntaxList<Diagnostic> diagnostics, string tag)
        : base(nodes, diagnostics)
    {
        Tag = tag;
    }

    public string Tag { get; }
}

internal sealed class FakeSyntaxParser : SyntaxParser<FakeSyntaxNode>
{
    private readonly string tag;

    public FakeSyntaxParser(string tag = "from-parse-core")
    {
        this.tag = tag;
    }

    public FakeSyntaxParser(SyntaxParserOptions<FakeSyntaxNode> options, string tag = "from-parse-core")
        : base(options)
    {
        this.tag = tag;
    }

    protected override SyntaxParserResult<FakeSyntaxNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var nodes = new List<FakeSyntaxNode>();
        var diagnostics = new List<Diagnostic>();
        var offset = 0;

        foreach (var segment in source.Text.Split(';'))
        {
            var location = FakeLocations.Segment(offset, segment);
            if (segment == "!")
            {
                diagnostics.Add(new FakeDiagnostic
                {
                    Message = "bad segment",
                    Location = location,
                    Severity = DiagnosticSeverity.Error,
                });
            }
            else if (segment.Length > 0)
            {
                nodes.Add(new FakeSyntaxNode { Content = segment, Location = location });
            }

            offset += segment.Length + 1;
        }

        return new FakeSyntaxParserResult(
            new SyntaxList<FakeSyntaxNode>(nodes.ToArray()),
            diagnostics.Count == 0 ? SyntaxList<Diagnostic>.Empty : new SyntaxList<Diagnostic>(diagnostics.ToArray()),
            tag);
    }
}

internal sealed class FakeSyntaxAnalyzer : SyntaxAnalyzer<FakeSyntaxNode>
{
    private readonly string message;

    public FakeSyntaxAnalyzer(string message)
    {
        this.message = message;
    }

    public override void Analyze(SyntaxAnalyzerContext<FakeSyntaxNode> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.ReportDiagnostic(new FakeDiagnostic
        {
            Message = message,
            Location = FakeLocations.Segment(0, string.Empty),
            Severity = DiagnosticSeverity.Warning,
        });
    }
}

// Echoes the node contents it was handed, so tests can pin that the pipeline passes the parsed
// nodes (not an empty or stale list) to analyzers.
internal sealed class NodeEchoAnalyzer : SyntaxAnalyzer<FakeSyntaxNode>
{
    public override void Analyze(SyntaxAnalyzerContext<FakeSyntaxNode> context)
    {
        var contents = new string[context.Nodes.Count];
        for (var index = 0; index < context.Nodes.Count; index++)
        {
            contents[index] = context.Nodes[index].Content;
        }

        context.ReportDiagnostic(new FakeDiagnostic
        {
            Message = "nodes:" + string.Join(",", contents),
            Location = FakeLocations.Segment(0, string.Empty),
            Severity = DiagnosticSeverity.Information,
        });
    }
}

// Never returns on its own: exits only when the analysis token (the caller's token linked with
// AnalyzerTimeout) fires, so tests can pin the timeout path deterministically.
internal sealed class BlockingSyntaxAnalyzer : SyntaxAnalyzer<FakeSyntaxNode>
{
    public override void Analyze(SyntaxAnalyzerContext<FakeSyntaxNode> context)
    {
        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
        }
    }
}

internal sealed record FakeBlockNode : SyntaxNode
{
    public required string Name { get; init; }

    public required string Content { get; init; }

    public override int RawKind => 1;
}

internal sealed record FakeAggregateResult : AggregateSyntaxParserResult<FakeBlockNode>
{
    public FakeAggregateResult(SyntaxList<FakeBlockNode> nodes, SyntaxList<Diagnostic> diagnostics, string tag)
        : base(nodes, diagnostics)
    {
        Tag = tag;
    }

    public string Tag { get; }
}

internal sealed class FakeAggregateParser : AggregateSyntaxParser<FakeBlockNode>
{
    public FakeAggregateParser()
    {
    }

    public FakeAggregateParser(AggregateSyntaxParserOptions<FakeBlockNode> options)
        : base(options)
    {
    }

    protected override SyntaxParserResult<FakeBlockNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
    {
        var nodes = new List<FakeBlockNode>();
        var offset = 0;

        foreach (var block in source.Text.Split('|'))
        {
            var separator = block.IndexOf(':');
            var name = block.Substring(0, separator);
            var content = block.Substring(separator + 1);
            nodes.Add(new FakeBlockNode
            {
                Name = name,
                Content = content,
                Location = FakeLocations.Segment(offset, block),
            });
            offset += block.Length + 1;
        }

        return new FakeAggregateResult(new SyntaxList<FakeBlockNode>(nodes.ToArray()), SyntaxList<Diagnostic>.Empty, "from-parse-core");
    }

    protected override SyntaxSource? GetSyntaxSource(FakeBlockNode node)
        => node.Name == "opaque"
            ? null
            : new SyntaxSource { Text = node.Content, Name = node.Name };
}

internal sealed class FakeBlockAnalyzer : SyntaxAnalyzer<FakeBlockNode>
{
    public override void Analyze(SyntaxAnalyzerContext<FakeBlockNode> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.ReportDiagnostic(new FakeDiagnostic
        {
            Message = "from-analyzer",
            Location = FakeLocations.Segment(0, string.Empty),
            Severity = DiagnosticSeverity.Warning,
        });
    }
}

internal static class FakeLocations
{
    public static SourceLocation Segment(int offset, string source)
        => new(
            new Position(offset, 1, offset + 1),
            new Position(offset + source.Length, 1, offset + source.Length + 1),
            source);
}
