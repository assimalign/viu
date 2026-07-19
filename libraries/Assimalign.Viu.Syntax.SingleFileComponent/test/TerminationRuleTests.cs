using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The column-0 termination rule (docs/FORMAT.md): a block closes at the first line whose first column is
// '}'. Because the parser only slices — it never looks inside content — braces in C# strings, nested CSS
// braces, HTML text with braces, and lines that resemble a block opener are all preserved verbatim as
// long as content is indented. The final test pins the flip side: an un-indented '}' closes the block
// early, which is exactly why the rule requires content to be indented.
public class TerminationRuleTests
{
    [Fact]
    public void Parse_ScriptWithLiteralBracesInStrings_DoesNotCloseEarly()
    {
        var source =
            "@script {\n" +
            "    var json = \"{ ok }\";\n" +
            "    var brace = \"}\";\n" +
            "}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Script.ShouldNotBeNull();
        descriptor.Script!.Content.ShouldBe("    var json = \"{ ok }\";\n    var brace = \"}\";\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_StyleWithNestedIndentedBraces_DoesNotCloseEarly()
    {
        var source =
            "@style {\n" +
            "    .a {\n" +
            "        color: red;\n" +
            "    }\n" +
            "    .b { color: blue; }\n" +
            "}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Styles.Count.ShouldBe(1);
        descriptor.Styles[0].Content.ShouldBe("    .a {\n        color: red;\n    }\n    .b { color: blue; }\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_TemplateWithBracesInText_DoesNotCloseEarly()
    {
        var source =
            "@template {\n" +
            "    <p>Use { and } carefully</p>\n" +
            "    <pre>function() { return {}; }</pre>\n" +
            "}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe(
            "    <p>Use { and } carefully</p>\n    <pre>function() { return {}; }</pre>\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_IndentedContentResemblingABlockOpener_IsPreservedAsContent()
    {
        // An indented "@template {" is content, not a new block: inside a block the parser only looks for a
        // column-0 '}'. So there is exactly one template and no duplicate diagnostic.
        var source =
            "@template {\n" +
            "    @template {\n" +
            "    <div>x</div>\n" +
            "}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe("    @template {\n    <div>x</div>\n");
        descriptor.CustomBlocks.Count.ShouldBe(0);
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_ClosingBraceWithTrailingText_ClosesTheBlock()
    {
        // Recognition is "first column is '}'"; anything after the '}' on that line is ignored.
        var source = "@script {\n    x\n} // done\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Script.ShouldNotBeNull();
        descriptor.Script!.Content.ShouldBe("    x\n");
        descriptor.Script!.Location.Source.ShouldBe("@script {\n    x\n}");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_IndentedHeaderAtTopLevel_IsStrayNotABlock()
    {
        // Headers are recognised only at column 0 (symmetric with the column-0 closer). An indented
        // "@template {" at the top level is therefore stray content, not a block opener.
        var source = "    @template {\n    <x/>\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldBeNull();
        result.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.StrayTopLevelContent);
    }

    [Fact]
    public void Parse_UnindentedContentBrace_ClosesEarly_DocumentingTheIndentRequirement()
    {
        // A CSS rule whose own '}' sits at column 0 terminates the block prematurely; the real closing '}'
        // then becomes stray top-level content. This is the documented reason content must be indented.
        var source =
            "@style {\n" +
            ".a {\n" +
            "color: red;\n" +
            "}\n" +
            "}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Content.ShouldBe(".a {\ncolor: red;\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.StrayTopLevelContent);
    }
}
