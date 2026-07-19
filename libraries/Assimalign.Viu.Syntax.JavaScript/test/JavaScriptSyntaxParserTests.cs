using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.JavaScript;

// Scaffold-level pins for the JavaScript parser: the pipeline plumbing and the incremental-caching
// contract (value-equatable results) must hold from the first commit, so statement-level parsing
// extends a tested seam instead of establishing one.
public class JavaScriptSyntaxParserTests
{
    [Fact]
    public void ParseSyntax_Program_ProducesRootSpanningWholeSource()
    {
        var parser = new JavaScriptSyntaxParser();
        var source = "export function mount(selector) {\n  return document.querySelector(selector);\n}\n";

        var result = parser.ParseSyntax(source);

        result.Diagnostics.Count.ShouldBe(0);
        var program = result.Nodes.ShouldHaveSingleItem().ShouldBeOfType<JavaScriptProgramNode>();
        program.Kind.ShouldBe(JavaScriptSyntaxNodeKind.Program);
        program.RawKind.ShouldBe((int)JavaScriptSyntaxNodeKind.Program);
        program.Content.ShouldBe(source);
        program.Location.Start.Offset.ShouldBe(0);
        program.Location.End.Offset.ShouldBe(source.Length);
        // The exact-slice invariant of SourceLocation.
        program.Location.Source.ShouldBe(source);
    }

    [Fact]
    public void ParseSyntax_SameSourceTwice_YieldsEqualResults()
    {
        var parser = new JavaScriptSyntaxParser();
        var source = "console.log('viu');";

        var first = parser.ParseSyntax(source);
        var second = parser.ParseSyntax(source);

        // The incremental-caching contract: equal input yields equal, equally-hashed results.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
