using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The two termination rules of the [V01.01.06.10] hybrid container (docs/FORMAT.md): an @-block
// (@script, custom, legacy) closes at the first line whose first column is '}' — column 0 is
// structural, so indented content with braces never closes early — while a tag block
// (<template>/<style>) closes at its matching end tag anywhere, indentation-free, using the .vue
// boundary rules. The last @-test pins the flip side: an un-indented '}' closes the @-block early,
// which is exactly why the @-rule requires content to be indented.
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
    public void Parse_TagStyleWithUnindentedBraces_DoesNotCloseEarly()
    {
        // A tag style is raw text to its matching </style> — CSS braces at column 0 are irrelevant,
        // unlike inside an @-block. This is the authoring hazard the tag container removes.
        var source =
            "<style>\n" +
            ".a {\n" +
            "color: red;\n" +
            "}\n" +
            ".b { color: blue; }\n" +
            "</style>\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Styles.Count.ShouldBe(1);
        descriptor.Styles[0].Content.ShouldBe("\n.a {\ncolor: red;\n}\n.b { color: blue; }\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_TagTemplateWithBracesInText_DoesNotCloseEarly()
    {
        var source =
            "<template>\n" +
            "    <p>Use { and } carefully</p>\n" +
            "    <pre>function() { return {}; }</pre>\n" +
            "</template>\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe(
            "\n    <p>Use { and } carefully</p>\n    <pre>function() { return {}; }</pre>\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_TagBlockClosingTag_MayBeIndented()
    {
        // Tag blocks close at their matching end tag anywhere — the column-0 rule applies only to
        // @-blocks, matching .vue semantics.
        var source = "<template>\n    <div/>\n    </template>\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe("\n    <div/>\n    ");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_IndentedContentResemblingABlockOpener_IsPreservedAsContent()
    {
        // An indented "@script {" is content, not a new block: inside an @-block the parser only looks
        // for a column-0 '}'. So there is exactly one script and no duplicate diagnostic.
        var source =
            "@script {\n" +
            "    @script {\n" +
            "    var x = 1;\n" +
            "}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Script.ShouldNotBeNull();
        descriptor.Script!.Content.ShouldBe("    @script {\n    var x = 1;\n");
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
    public void Parse_IndentedHeadersAtTopLevel_AreStrayNotBlocks()
    {
        // Both containers open at column 0 only (symmetric with the column-0 @-closer). Indented
        // "@script {" and "<template>" at the top level are therefore stray content, not block openers.
        var atSource = "    @script {\n    x\n}\n";
        var tagSource = "    <template>\n    <x/>\n    </template>\n";

        var atResult = SingleFileComponentParser.Parse(atSource);
        var tagResult = SingleFileComponentParser.Parse(tagSource);

        atResult.Descriptor.Script.ShouldBeNull();
        atResult.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.StrayTopLevelContent);
        tagResult.Descriptor.Template.ShouldBeNull();
        tagResult.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.StrayTopLevelContent);
    }

    [Fact]
    public void Parse_UnindentedContentBrace_ClosesEarly_DocumentingTheIndentRequirement()
    {
        // C# whose own '}' sits at column 0 terminates the @-block prematurely; the real closing '}'
        // then becomes stray top-level content. This is the documented reason @-block content must be
        // indented (tag blocks have no such requirement — see the tag tests above).
        var source =
            "@script {\n" +
            "if (ready) {\n" +
            "Go();\n" +
            "}\n" +
            "}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("if (ready) {\nGo();\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.StrayTopLevelContent);
    }
}
