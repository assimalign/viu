using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The [V01.01.06.10] canonical tag containers in .viu: <template> closes on the Vue nested-markup
// boundary (attributes, comments, and nested raw-text elements cannot close it) and <style> is raw
// text to its matching end tag — the exact rules the .vue engine uses, shared through
// SingleFileComponentTagScanner. Upstream references:
// https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts and
// https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-core/src/tokenizer.ts.
public class TagBlockParsingTests
{
    [Fact]
    public void Parse_TemplateNestedMarkup_IgnoresClosingTextInAttributesCommentsAndRawElements()
    {
        // A nested <style> (or <script>) inside the template is a raw-text element, so end-tag-shaped
        // text inside it cannot close the root template — hybrid .viu matches .vue exactly.
        var source =
            "<template>\n" +
            "    <div data-example=\"</template>\">\n" +
            "        <!-- </template> -->\n" +
            "        <style>.raw { content: \"</template>\"; }</style>\n" +
            "        <template><span /></template>\n" +
            "    </div>\n" +
            "</template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe(
            "\n    <div data-example=\"</template>\">\n" +
            "        <!-- </template> -->\n" +
            "        <style>.raw { content: \"</template>\"; }</style>\n" +
            "        <template><span /></template>\n" +
            "    </div>\n");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_TemplateWithNonHtmlLang_IsRawText()
    {
        // lang != "html" makes the template raw text (Vue's preprocessed-template rule), so nested
        // tag-shaped text is not interpreted.
        var source = "<template lang=\"pug\">div\n  template this is text\n</template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Lang.ShouldBe("pug");
        result.Descriptor.Template!.Content.ShouldBe("div\n  template this is text\n");
    }

    [Fact]
    public void Parse_StyleIsRawText_TagShapedContentDoesNotOpenBlocks()
    {
        var source = "<style>\n    .a::before { content: \"<template>\"; }\n</style>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldBeNull();
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Content.ShouldBe("\n    .a::before { content: \"<template>\"; }\n");
    }

    [Fact]
    public void Parse_SelfClosingTagBlocks_YieldEmptyBlocks()
    {
        var source = "<template />\n<style scoped />\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe(string.Empty);
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Content.ShouldBe(string.Empty);
        result.Descriptor.Styles[0].Scoped.ShouldBeTrue();
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_InlineSameLineContent_IsSliced()
    {
        var source = "<template><div>{{ x }}</div></template>\n<style>.a { color: red; }</style>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template!.Content.ShouldBe("<div>{{ x }}</div>");
        result.Descriptor.Styles[0].Content.ShouldBe(".a { color: red; }");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_OpeningTagAttributeContainingClosingText_DoesNotEndTheTag()
    {
        var source = "<template data-example=\"</template> >\" other='a>b'><div /></template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe("<div />");
        result.Descriptor.Template.GetOptionValue("data-example").ShouldBe("</template> >");
        result.Descriptor.Template.GetOptionValue("other").ShouldBe("a>b");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_TopLevelComment_IsToleratedBetweenBlocks()
    {
        var source =
            "<!-- header comment -->\n" +
            "<template><p/></template>\n" +
            "<!-- a\n     multi-line comment -->\n" +
            "@script {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Script.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_TrailingContentAfterClosingTag_IsStray()
    {
        // Blocks open at column 0 only, so a tag block that closes mid-line must be followed by only
        // whitespace on that line; a non-whitespace remainder is stray top-level content.
        var source = "<template><p/></template> junk\n@script {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Script.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.StrayTopLevelContent);
        result.Errors[0].Location.Source.ShouldBe("junk");
    }

    [Fact]
    public void Parse_AtHeaderShapedLineInsideTagTemplate_IsContent()
    {
        // Inside a tag block the @-rule does not apply: a column-0 "@script {" line inside <template>
        // is template content, not a block opener.
        var source = "<template>\n@script {\n}\n</template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Script.ShouldBeNull();
        result.Descriptor.Template!.Content.ShouldBe("\n@script {\n}\n");
    }
}
