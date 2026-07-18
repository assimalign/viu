using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Html;

// Scaffold-level pins for the plain-HTML parser: the pipeline plumbing and the incremental-caching
// contract (value-equatable results) must hold from the first commit, so element-level parsing extends
// a tested seam instead of establishing one.
public class HtmlSyntaxParserTests
{
    [Fact]
    public void ParseSyntax_Document_ProducesRootSpanningWholeSource()
    {
        var parser = new HtmlSyntaxParser();
        var source = "<!DOCTYPE html>\n<html>\n<body></body>\n</html>\n";

        var result = parser.ParseSyntax(source);

        result.Diagnostics.Count.ShouldBe(0);
        var document = result.Nodes.ShouldHaveSingleItem().ShouldBeOfType<HtmlDocumentNode>();
        document.Kind.ShouldBe(HtmlSyntaxNodeKind.Document);
        document.RawKind.ShouldBe((int)HtmlSyntaxNodeKind.Document);
        document.Content.ShouldBe(source);
        document.Location.Start.Offset.ShouldBe(0);
        document.Location.End.Offset.ShouldBe(source.Length);
        // The exact-slice invariant of SourceLocation.
        document.Location.Source.ShouldBe(source);
    }

    [Fact]
    public void ParseSyntax_SameSourceTwice_YieldsEqualResults()
    {
        var parser = new HtmlSyntaxParser();
        var source = "<main id=\"app\"></main>";

        var first = parser.ParseSyntax(source);
        var second = parser.ParseSyntax(source);

        // The incremental-caching contract: equal input yields equal, equally-hashed results.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
