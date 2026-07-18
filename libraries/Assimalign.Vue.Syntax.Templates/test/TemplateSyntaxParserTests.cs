using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// Pins the TemplateSyntaxParser adapter contract ([V01.01.05.09]): parsing semantics are exactly
// TemplateParser.Parse (the upstream-pinned baseParse port stays authoritative), recoverable OnError
// errors additionally surface as the result's uniform Diagnostics, a caller-supplied OnError still
// sees every error, and the caller's ParserOptions instance is never mutated.
public class TemplateSyntaxParserTests
{
    [Fact]
    public void ParseTemplate_Root_EqualsTheStaticParserOutput()
    {
        var source = "<div id=\"a\">{{ message }}</div>";

        var expected = TemplateParser.Parse(source);
        var result = new TemplateSyntaxParser().ParseTemplate(source);

        // Value equality across independent parses — the incremental-caching contract.
        result.Root.ShouldBe(expected);
        result.Nodes.ShouldHaveSingleItem().ShouldBe(expected);
        result.Diagnostics.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseTemplate_HtmlModeOptions_FlowThroughTheClone()
    {
        var source = "<br>text";

        var expected = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        var result = new TemplateSyntaxParser(ParserOptions.CreateHtml()).ParseTemplate(source);

        result.Root.ShouldBe(expected);
    }

    [Fact]
    public void ParseTemplate_MalformedInput_SurfacesCompilerErrorsAsDiagnostics()
    {
        // Unterminated tag: upstream reports EOF_IN_TAG (recoverable, never throws).
        var result = new TemplateSyntaxParser().ParseTemplate("<div");

        result.Diagnostics.Count.ShouldBeGreaterThan(0);
        foreach (var diagnostic in result.Diagnostics)
        {
            var error = diagnostic.ShouldBeOfType<CompilerError>();
            error.Severity.ShouldBe(DiagnosticSeverity.Error);
            error.RawCode.ShouldBe((int)error.Code);
        }
    }

    [Fact]
    public void ParseTemplate_CallerOnError_StillSeesEveryErrorAndOptionsAreNotMutated()
    {
        var received = new List<CompilerError>();
        var options = new ParserOptions { OnError = received.Add };
        var callerOnError = options.OnError;
        var parser = new TemplateSyntaxParser(options);

        var result = parser.ParseTemplate("<div");

        received.Count.ShouldBe(result.Diagnostics.Count);
        received.Count.ShouldBeGreaterThan(0);
        // The parser intercepts on a per-parse clone; the caller's instance keeps its own delegate.
        options.OnError.ShouldBeSameAs(callerOnError);
    }

    [Fact]
    public void ParseTemplate_EqualInput_YieldsEqualResults()
    {
        var parser = new TemplateSyntaxParser();
        var source = "<span v-if=\"visible\">{{ value }}</span>";

        var first = parser.ParseTemplate(source);
        var second = parser.ParseTemplate(source);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Parse_UntypedEntryPoint_ReturnsTheTemplateResult()
    {
        SyntaxParser parser = new TemplateSyntaxParser();

        var result = parser.Parse("<p>text</p>");

        var template = result.ShouldBeOfType<TemplateSyntaxParserResult>();
        template.Root.NodeType.ShouldBe(NodeType.Root);
    }

    [Fact]
    public void RawKind_ProjectsTheUpstreamPinnedNodeType()
    {
        var root = TemplateParser.Parse("<div>{{ value }}</div>");

        // The language-agnostic SyntaxNode.RawKind projection is the NodeType numeric value, which
        // is itself pinned to @vue/compiler-core's NodeTypes.
        root.RawKind.ShouldBe((int)NodeType.Root);
        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.RawKind.ShouldBe((int)NodeType.Element);
        element.Children.ShouldHaveSingleItem().RawKind.ShouldBe((int)NodeType.Interpolation);
    }
}
