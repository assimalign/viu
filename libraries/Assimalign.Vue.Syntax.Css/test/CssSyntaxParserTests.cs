using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Css;

// Scaffold-level pins for the CSS parser: the pipeline plumbing and the incremental-caching contract
// (value-equatable results) must hold from the first commit, so rule-level parsing
// ([V01.01.06.04]/[V01.01.06.06]) extends a tested seam instead of establishing one.
public class CssSyntaxParserTests
{
    [Fact]
    public void ParseSyntax_Stylesheet_ProducesRootSpanningWholeSource()
    {
        var parser = new CssSyntaxParser();
        var source = "a:hover {\n  color: red;\n}\n";

        var result = parser.ParseSyntax(source);

        result.Diagnostics.Count.ShouldBe(0);
        var stylesheet = result.Nodes.ShouldHaveSingleItem().ShouldBeOfType<CssStylesheetNode>();
        stylesheet.Kind.ShouldBe(CssSyntaxNodeKind.Stylesheet);
        stylesheet.RawKind.ShouldBe((int)CssSyntaxNodeKind.Stylesheet);
        stylesheet.Content.ShouldBe(source);
        stylesheet.Location.Start.Offset.ShouldBe(0);
        stylesheet.Location.End.Offset.ShouldBe(source.Length);
        // The exact-slice invariant of SourceLocation.
        stylesheet.Location.Source.ShouldBe(source);
    }

    [Fact]
    public void ParseSyntax_SameSourceTwice_YieldsEqualResults()
    {
        var parser = new CssSyntaxParser();
        var source = ".button { padding: 1rem; }";

        var first = parser.ParseSyntax(source);
        var second = parser.ParseSyntax(source);

        // The incremental-caching contract: equal input yields equal, equally-hashed results.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Parse_UntypedEntryPoint_ReturnsSameNodes()
    {
        SyntaxParser parser = new CssSyntaxParser();
        var source = "body { margin: 0; }";

        var result = parser.Parse(new SyntaxSource { Text = source, Name = "site.css", Language = "css" });

        result.ShouldBeOfType<SyntaxParserResult<CssSyntaxNode>>();
        result.Nodes.ShouldHaveSingleItem().ShouldBeOfType<CssStylesheetNode>();
    }
}
