using System;
using System.Threading;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax;

// Pins the shared SyntaxParser<T> pipeline contract ([V01.01.05.09]): ParseCore's nodes and parse
// diagnostics flow through unchanged, analyzers append after them in registration order, the derived
// result's runtime type survives the append (the with-clone), and results stay value-equatable — the
// incremental-caching prerequisite every derived parser relies on.
public class SyntaxParserTests
{
    [Fact]
    public void ParseSyntax_Segments_ProducesNodesAndParseDiagnostics()
    {
        var parser = new FakeSyntaxParser();

        var result = parser.ParseSyntax("a;!;b");

        result.Nodes.Count.ShouldBe(2);
        result.Nodes[0].Content.ShouldBe("a");
        result.Nodes[1].Content.ShouldBe("b");
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Message.ShouldBe("bad segment");
        diagnostic.RawCode.ShouldBe(1);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ParseSyntax_WithAnalyzers_AppendsDiagnosticsInRegistrationOrderAndKeepsResultType()
    {
        var options = new SyntaxParserOptions<FakeSyntaxNode>();
        options.Analyzers.Add(new FakeSyntaxAnalyzer("first-analyzer"));
        options.Analyzers.Add(new FakeSyntaxAnalyzer("second-analyzer"));
        var parser = new FakeSyntaxParser(options);

        var result = parser.ParseSyntax("a;!");

        result.Diagnostics.Count.ShouldBe(3);
        result.Diagnostics[0].Message.ShouldBe("bad segment");
        result.Diagnostics[1].Message.ShouldBe("first-analyzer");
        result.Diagnostics[2].Message.ShouldBe("second-analyzer");
        // The with-clone that appends analyzer diagnostics must preserve the derived result record.
        var derived = result.ShouldBeOfType<FakeSyntaxParserResult>();
        derived.Tag.ShouldBe("from-parse-core");
        derived.Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_UntypedEntryPoint_RunsTheSamePipeline()
    {
        var options = new SyntaxParserOptions<FakeSyntaxNode>();
        options.Analyzers.Add(new FakeSyntaxAnalyzer("from-analyzer"));
        SyntaxParser parser = new FakeSyntaxParser(options);

        var result = parser.Parse(new SyntaxSource { Text = "a;b", Name = "fake", Language = "fake" });

        var derived = result.ShouldBeOfType<FakeSyntaxParserResult>();
        derived.Nodes.Count.ShouldBe(2);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("from-analyzer");
    }

    [Fact]
    public void ParseSyntax_PreCancelledToken_WithAnalyzers_Throws()
    {
        var options = new SyntaxParserOptions<FakeSyntaxNode>();
        options.Analyzers.Add(new FakeSyntaxAnalyzer("never-reported"));
        var parser = new FakeSyntaxParser(options);

        Should.Throw<OperationCanceledException>(() => parser.ParseSyntax("a", new CancellationToken(canceled: true)));
    }

    [Fact]
    public void ParseSyntax_AnalyzerExceedingTimeout_Throws()
    {
        var options = new SyntaxParserOptions<FakeSyntaxNode>
        {
            AnalyzerTimeout = TimeSpan.FromMilliseconds(50),
        };
        options.Analyzers.Add(new BlockingSyntaxAnalyzer());
        var parser = new FakeSyntaxParser(options);

        // The blocking analyzer only exits when the linked timeout token fires, so this pins that
        // AnalyzerTimeout actually bounds the analysis pass.
        Should.Throw<OperationCanceledException>(() => parser.ParseSyntax("a"));
    }

    [Fact]
    public void ParseSyntax_Analyzers_ReceiveTheParsedNodes()
    {
        var options = new SyntaxParserOptions<FakeSyntaxNode>();
        options.Analyzers.Add(new NodeEchoAnalyzer());
        var parser = new FakeSyntaxParser(options);

        var result = parser.ParseSyntax("a;b;c");

        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("nodes:a,b,c");
    }

    [Fact]
    public void ParseSyntax_EqualInput_YieldsEqualResults()
    {
        var parser = new FakeSyntaxParser();

        var first = parser.ParseSyntax("a;b;c");
        var second = parser.ParseSyntax("a;b;c");

        // The incremental-caching contract: equal input yields equal, equally-hashed results.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(parser.ParseSyntax("a;b;d"));
    }

    [Fact]
    public void Parse_NullInput_Throws()
    {
        var parser = new FakeSyntaxParser();

        Should.Throw<ArgumentNullException>(() => parser.Parse((string)null!));
        Should.Throw<ArgumentNullException>(() => parser.Parse((SyntaxSource)null!));
        Should.Throw<ArgumentNullException>(() => parser.ParseSyntax((string)null!));
        Should.Throw<ArgumentNullException>(() => parser.ParseSyntax((SyntaxSource)null!));
    }
}
