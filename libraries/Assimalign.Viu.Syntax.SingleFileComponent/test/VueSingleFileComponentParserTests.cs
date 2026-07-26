using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// Pins the [V01.01.06.09] tag-container compatibility grammar to Vue 3.5's SFC root parsing model.
/// Upstream references:
/// https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts and
/// https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-core/src/tokenizer.ts.
/// </summary>
public class VueSingleFileComponentParserTests
{
    [Fact]
    public void Parse_AllSupportedBlockKinds_PreservesContentAttributesAndOrder()
    {
        var source =
            "<template lang=\"html\"><main /></template>\n" +
            "<script setup lang='csharp'>public string Title = \"Hello\";</script>\n" +
            "<style scoped module=\"classes\">.first { color: red; }</style>\n" +
            "<style lang=css>.second { color: blue; }</style>\n" +
            "<docs locale=\"en-US\">Documentation</docs>\n";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe("<main />");
        result.Descriptor.Template.Lang.ShouldBe("html");
        result.Descriptor.Script.ShouldBeNull();
        result.Descriptor.ScriptSetup.ShouldNotBeNull();
        result.Descriptor.ScriptSetup!.Content.ShouldBe("public string Title = \"Hello\";");
        result.Descriptor.ScriptSetup.HasOption("setup").ShouldBeTrue();
        result.Descriptor.ScriptSetup.Lang.ShouldBe("csharp");
        result.Descriptor.Styles.Count.ShouldBe(2);
        result.Descriptor.Styles[0].Scoped.ShouldBeTrue();
        result.Descriptor.Styles[0].ModuleName.ShouldBe("classes");
        result.Descriptor.Styles[1].Lang.ShouldBe("css");
        result.Descriptor.CustomBlocks.Count.ShouldBe(1);
        result.Descriptor.CustomBlocks[0].Name.ShouldBe("docs");
        result.Descriptor.CustomBlocks[0].GetOptionValue("locale").ShouldBe("en-US");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_OpeningTagAttributeContainsClosingText_DoesNotEndOpeningTagOrTemplate()
    {
        var source =
            "<template data-example=\"</template> >\" other='a>b'>" +
            "<div />" +
            "</template>";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe("<div />");
        result.Descriptor.Template.GetOptionValue("data-example").ShouldBe("</template> >");
        result.Descriptor.Template.GetOptionValue("other").ShouldBe("a>b");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_TemplateNestedMarkup_IgnoresClosingTextInAttributesCommentsAndRawElements()
    {
        var source =
            "<template>" +
            "<div data-example=\"</template>\">" +
            "<!-- </template> -->" +
            "<script>const example = \"</template>\";</script>" +
            "<template><span /></template>" +
            "</div>" +
            "</template>" +
            "<style>.card { display: block; }</style>";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe(
            "<div data-example=\"</template>\">" +
            "<!-- </template> -->" +
            "<script>const example = \"</template>\";</script>" +
            "<template><span /></template>" +
            "</div>");
        result.Descriptor.Styles.Count.ShouldBe(1);
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_NonTemplateRootBlock_TreatsOtherTagsAsRawContent()
    {
        var source =
            "<script>var markup = \"<style>not a block</style>\";</script>" +
            "<docs><template>raw documentation</template></docs>";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("var markup = \"<style>not a block</style>\";");
        result.Descriptor.Styles.Count.ShouldBe(0);
        result.Descriptor.CustomBlocks.Count.ShouldBe(1);
        result.Descriptor.CustomBlocks[0].Content.ShouldBe("<template>raw documentation</template>");
    }

    [Fact]
    public void Parse_PreprocessedTemplate_TreatsNestedTagsAsRawContent()
    {
        var source =
            "<template lang=\"pug\">div\n  template this is text\n</template>" +
            "<style />";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe("div\n  template this is text\n");
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_DuplicateSingletonBlocks_KeepsFirstAndReportsEachDuplicate()
    {
        var source =
            "<template>first</template>" +
            "<template>second</template>" +
            "<script>first</script>" +
            "<script>second</script>" +
            "<script setup>setup first</script>" +
            "<script setup>setup second</script>";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Descriptor.Template!.Content.ShouldBe("first");
        result.Descriptor.Script!.Content.ShouldBe("first");
        result.Descriptor.ScriptSetup!.Content.ShouldBe("setup first");
        result.Errors.Count.ShouldBe(3);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.DuplicateTemplateBlock).ShouldBe(1);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.DuplicateScriptBlock).ShouldBe(1);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.DuplicateScriptSetupBlock).ShouldBe(1);
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_UnclosedBlock_PreservesContentToEndAndReportsOpeningTag()
    {
        var source = "<style scoped>\r\n.card {\r\n  display: grid;\r\n}";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Content.ShouldBe("\r\n.card {\r\n  display: grid;\r\n}");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.UnterminatedTagBlock);
        result.Errors[0].Location.Source.ShouldBe("<style scoped>");
        result.Descriptor.Styles[0].ContentLocation.Start.ShouldBe(new Position(14, 1, 15));
        result.Descriptor.Styles[0].ContentLocation.End.ShouldBe(new Position(source.Length, 4, 2));
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_MalformedTopLevelConstructs_ReportsDiagnosticsAndContinues()
    {
        var source =
            "stray text\n" +
            "</template>\n" +
            "<1invalid>\n" +
            "<style scoped scoped>.card { color: red; }</style>";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.StrayTopLevelContent);
        result.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.UnexpectedClosingTag);
        result.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.MalformedTagBlock);
        result.Errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.DuplicateTagAttribute);
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_UnterminatedQuotedAttribute_ReportsWithoutThrowing()
    {
        var source = "<template example=\"</template>";

        var result = Should.NotThrow(() => VueSingleFileComponentParser.Parse(source));

        result.Descriptor.Template.ShouldBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.MalformedTagAttribute);
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_SameSourceTwice_ProducesStructurallyEqualResults()
    {
        var source = "<template><p>Hello</p></template><style scoped>.p { color: red; }</style>";

        VueSingleFileComponentParser.Parse(source).ShouldBe(VueSingleFileComponentParser.Parse(source));
    }

    [Fact]
    public void Parse_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() => VueSingleFileComponentParser.Parse(null!));
    }
}
