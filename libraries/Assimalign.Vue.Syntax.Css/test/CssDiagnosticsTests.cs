using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Css;

// CSS Syntax Module Level 3 error recovery (https://www.w3.org/TR/css-syntax-3/#error-handling): malformed
// input never throws — it reports a Vuecs-defined CssError and the parser resynchronizes, still producing
// as much of the tree as it can. The exact-slice invariant holds for the recovered tree too.
public class CssDiagnosticsTests
{
    [Fact]
    public void Parse_UnterminatedBlock_ReportsDiagnostic_AndStillProducesRule()
    {
        var source = "a { color: red";

        var result = new CssSyntaxParser().ParseSyntax(source);

        var error = result.Diagnostics.ShouldHaveSingleItem().ShouldBeOfType<CssError>();
        error.Code.ShouldBe(CssErrorCode.UnterminatedBlock);
        error.Severity.ShouldBe(DiagnosticSeverity.Error);
        error.RawCode.ShouldBe((int)CssErrorCode.UnterminatedBlock);

        var rule = ((CssStylesheetNode)result.Nodes[0]).Rules.ShouldHaveSingleItem().ShouldBeOfType<CssQualifiedRuleNode>();
        rule.Declarations[0].Property.ShouldBe("color");
    }

    [Fact]
    public void Parse_StrayRightBrace_ReportsDiagnostic_AndRecovers()
    {
        var source = "}\n.a { color: red; }\n";

        var result = new CssSyntaxParser().ParseSyntax(source);

        result.Diagnostics.OfType<CssError>().Select(e => e.Code)
            .ShouldContain(CssErrorCode.UnexpectedRightBrace);
        // Recovery: the well-formed rule after the stray brace is still parsed.
        ((CssStylesheetNode)result.Nodes[0]).Rules.OfType<CssQualifiedRuleNode>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_MissingDeclarationColon_DropsDeclaration_KeepsSiblings()
    {
        var source = ".a { color red; margin: 0; }";

        var result = new CssSyntaxParser().ParseSyntax(source);

        result.Diagnostics.OfType<CssError>().Select(e => e.Code)
            .ShouldContain(CssErrorCode.MissingDeclarationColon);
        var rule = (CssQualifiedRuleNode)((CssStylesheetNode)result.Nodes[0]).Rules[0];
        // The malformed "color red" is dropped; the well-formed "margin: 0" survives.
        rule.Declarations.ShouldHaveSingleItem().Property.ShouldBe("margin");
    }

    [Fact]
    public void Parse_UnterminatedString_ReportsDiagnostic()
    {
        var source = ".a { content: \"oops\n }";

        var result = new CssSyntaxParser().ParseSyntax(source);

        result.Diagnostics.OfType<CssError>().Select(e => e.Code)
            .ShouldContain(CssErrorCode.UnterminatedString);
    }

    [Fact]
    public void Parse_MalformedInput_DiagnosticSpansAndTreeAreExact()
    {
        var source = "oops }\n@media screen {\n  .a { color red }\n";

        var result = new CssSyntaxParser().ParseSyntax(source);

        // Never throws; all reported diagnostics carry exact source slices, and the recovered tree upholds
        // the exact-slice invariant.
        foreach (var diagnostic in result.Diagnostics)
        {
            var location = diagnostic.Location;
            location.Source.ShouldBe(source.Substring(location.Start.Offset, location.End.Offset - location.Start.Offset));
        }

        CssTestHelpers.AssertExactSlice((CssStylesheetNode)result.Nodes[0], source);
    }
}
