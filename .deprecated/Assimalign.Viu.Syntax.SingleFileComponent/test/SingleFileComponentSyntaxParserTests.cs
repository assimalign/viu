using System.Threading;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// Pins the SingleFileComponentSyntaxParser adapter contract ([V01.01.05.09]): block slicing is exactly
// SingleFileComponentParser.Parse (the @vue/compiler-sfc-parity static entry point stays
// authoritative), the node list restores source order, errors surface as the uniform Diagnostics, and
// each block reaches the aggregate registration seam as content + block name + lang option.
public class SingleFileComponentSyntaxParserTests
{
    private const string Component =
        "@template {\n" +
        "    <div>{{ message }}</div>\n" +
        "}\n" +
        "@style scoped lang=\"scss\" {\n" +
        "    .box { color: red; }\n" +
        "}\n" +
        "@script {\n" +
        "    public string Message = \"Hello\";\n" +
        "}\n" +
        "@docs {\n" +
        "    Usage notes.\n" +
        "}\n";

    [Fact]
    public void ParseComponent_Descriptor_EqualsTheStaticParserOutput()
    {
        var expected = SingleFileComponentParser.Parse(Component);
        var result = new SingleFileComponentSyntaxParser().ParseComponent(Component);

        // Value equality across independent parses — the incremental-caching contract.
        result.Descriptor.ShouldBe(expected.Descriptor);
        result.Diagnostics.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseComponent_Nodes_AreTheBlocksInSourceOrder()
    {
        var result = new SingleFileComponentSyntaxParser().ParseComponent(Component);

        // The descriptor groups blocks by kind; the node list restores source order.
        result.Nodes.Count.ShouldBe(4);
        result.Nodes[0].ShouldBeOfType<SingleFileComponentTemplateBlock>();
        result.Nodes[1].ShouldBeOfType<SingleFileComponentStyleBlock>();
        result.Nodes[2].ShouldBeOfType<SingleFileComponentScriptBlock>();
        result.Nodes[3].ShouldBeOfType<SingleFileComponentCustomBlock>().Name.ShouldBe("docs");

        // The language-agnostic SyntaxNode.RawKind projection is the block kind's numeric value.
        foreach (var block in result.Nodes)
        {
            block.RawKind.ShouldBe((int)block.Kind);
        }
    }

    [Fact]
    public void ParseComponent_Errors_SurfaceAsUniformDiagnostics()
    {
        // A second @template is a recoverable duplicate-block diagnostic (first block wins).
        var source =
            "@template {\n" +
            "    <div>one</div>\n" +
            "}\n" +
            "@template {\n" +
            "    <div>two</div>\n" +
            "}\n";

        var expected = SingleFileComponentParser.Parse(source);
        var result = new SingleFileComponentSyntaxParser().ParseComponent(source);

        expected.Errors.Count.ShouldBeGreaterThan(0);
        result.Diagnostics.Count.ShouldBe(expected.Errors.Count);
        foreach (var diagnostic in result.Diagnostics)
        {
            var error = diagnostic.ShouldBeOfType<SingleFileComponentError>();
            error.Severity.ShouldBe(DiagnosticSeverity.Error);
            error.RawCode.ShouldBe((int)error.Code);
        }
    }

    [Fact]
    public void ParseComponent_RegisteredParser_ReceivesContentBlockNameAndLang()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(static source => source.Name == "style", new CapturingParser());
        var parser = new SingleFileComponentSyntaxParser(options);

        var result = parser.ParseComponent(Component);

        var dispatched = result.SourceResults.ShouldHaveSingleItem();
        var style = dispatched.Node.ShouldBeOfType<SingleFileComponentStyleBlock>();
        dispatched.Source.Name.ShouldBe("style");
        dispatched.Source.Language.ShouldBe("scss");
        // The embedded source is the exact raw block content — this parser never re-slices it.
        dispatched.Source.Text.ShouldBe(style.Content);
        dispatched.Result.Nodes.ShouldHaveSingleItem()
            .ShouldBeOfType<CapturedNode>().Content.ShouldBe(style.Content);
    }

    [Fact]
    public void ParseComponent_EqualInput_YieldsEqualResults()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(static source => source.Name == "style", new CapturingParser());
        var parser = new SingleFileComponentSyntaxParser(options);

        var first = parser.ParseComponent(Component);
        var second = parser.ParseComponent(Component);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Parse_UntypedEntryPoint_ReturnsTheSingleFileComponentResult()
    {
        SyntaxParser parser = new SingleFileComponentSyntaxParser();

        var result = parser.Parse(Component);

        result.ShouldBeOfType<SingleFileComponentSyntaxParserResult>().Descriptor.Template.ShouldNotBeNull();
    }

    // A minimal registered parser standing in for a real language parser (e.g. the CSS parser): it
    // captures the embedded source it was handed so dispatch can be asserted.
    private sealed record CapturedNode : SyntaxNode
    {
        public required string Content { get; init; }

        public override int RawKind => 0;
    }

    private sealed class CapturingParser : SyntaxParser<CapturedNode>
    {
        protected override SyntaxParserResult<CapturedNode> ParseCore(SyntaxSource source, CancellationToken cancellationToken)
        {
            var node = new CapturedNode
            {
                Content = source.Text,
                Location = new SourceLocation(
                    new Position(0, 1, 1),
                    new Position(source.Text.Length, 1, source.Text.Length + 1),
                    source.Text),
            };

            return new SyntaxParserResult<CapturedNode>(new SyntaxList<CapturedNode>(new CapturedNode[] { node }));
        }
    }
}
