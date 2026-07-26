using System.Threading;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>Tests the tag-based aggregate registration and dispatch seam.</summary>
public class VueSingleFileComponentSyntaxParserTests
{
    [Fact]
    public void ParseComponent_RegisteredTemplateAndStyles_DispatchInBlockOrder()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(
            static source => source.Name == "template",
            new CapturingParser());
        options.RegisterParser(
            static source => source.Name == "style",
            new CapturingParser());
        var parser = new VueSingleFileComponentSyntaxParser(options);
        var source =
            "<style scoped>.first { color: red; }</style>" +
            "<template><div /></template>" +
            "<style module>.second { color: blue; }</style>";

        var result = parser.ParseComponent(source);

        result.Diagnostics.Count.ShouldBe(0);
        result.Nodes.Count.ShouldBe(3);
        result.Nodes[0].ShouldBeOfType<SingleFileComponentStyleBlock>();
        result.Nodes[1].ShouldBeOfType<SingleFileComponentTemplateBlock>();
        result.Nodes[2].ShouldBeOfType<SingleFileComponentStyleBlock>();
        result.SourceResults.Count.ShouldBe(3);
        result.SourceResults[0].Source.Text.ShouldBe(".first { color: red; }");
        result.SourceResults[1].Source.Text.ShouldBe("<div />");
        result.SourceResults[2].Source.Text.ShouldBe(".second { color: blue; }");
    }

    [Fact]
    public void ParseComponent_OrdinaryAndSetupScripts_PreservesBothWithoutDispatchRegistration()
    {
        var parser = new VueSingleFileComponentSyntaxParser();
        var source =
            "<script lang=\"csharp\">public int Count;</script>" +
            "<script setup lang=\"csharp\">public int Other;</script>";

        var result = parser.ParseComponent(source);

        result.Diagnostics.Count.ShouldBe(0);
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.ScriptSetup.ShouldNotBeNull();
        result.Nodes.Count.ShouldBe(2);
        result.SourceResults.Count.ShouldBe(0);
    }

    private sealed class CapturingParser : SyntaxParser<CapturedNode>
    {
        protected override SyntaxParserResult<CapturedNode> ParseCore(
            SyntaxSource source,
            CancellationToken cancellationToken)
            => new(new SyntaxList<CapturedNode>(new[]
            {
                new CapturedNode
                {
                    Location = new SourceLocation(
                        new Position(0, 1, 1),
                        new Position(source.Text.Length, 1, source.Text.Length + 1),
                        source.Text),
                },
            }));
    }

    private sealed record CapturedNode : SyntaxNode
    {
        public override int RawKind => 1;
    }
}
