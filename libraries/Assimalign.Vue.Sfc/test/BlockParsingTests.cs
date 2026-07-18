using Shouldly;

using Xunit;

namespace Assimalign.Vue.Sfc;

// The happy-path block slicing: a well-formed .viu file yields typed template/script/style/custom
// blocks with exact raw content. Block semantics mirror the Vue SFC spec
// (https://vuejs.org/api/sfc-spec.html); the @-block container is the Vuecs divergence (see docs/FORMAT.md).
public class BlockParsingTests
{
    private const string Component =
        "@template {\n" +
        "    <div>{{ message }}</div>\n" +
        "}\n" +
        "@script {\n" +
        "    public string Message = \"Hello\";\n" +
        "}\n" +
        "@style scoped {\n" +
        "    .box { color: red; }\n" +
        "}\n";

    [Fact]
    public void Parse_WellFormedComponent_ExposesEachBlock()
    {
        var descriptor = SfcTestHelpers.Parse(Component);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Script.ShouldNotBeNull();
        descriptor.Styles.Count.ShouldBe(1);
        descriptor.CustomBlocks.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_WellFormedComponent_ReportsNoErrors()
    {
        SfcTestHelpers.Errors(Component).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_Blocks_CarryTheirKindAndName()
    {
        var descriptor = SfcTestHelpers.Parse(Component);

        descriptor.Template!.Kind.ShouldBe(SfcBlockKind.Template);
        descriptor.Template!.Name.ShouldBe("template");
        descriptor.Script!.Kind.ShouldBe(SfcBlockKind.Script);
        descriptor.Script!.Name.ShouldBe("script");
        descriptor.Styles[0].Kind.ShouldBe(SfcBlockKind.Style);
        descriptor.Styles[0].Name.ShouldBe("style");
    }

    [Fact]
    public void Parse_Content_IsTheExactRawSliceIncludingTrailingNewline()
    {
        var descriptor = SfcTestHelpers.Parse(Component);

        // Content runs from the line after the header up to (not including) the closing-brace line, so it
        // keeps interior indentation and the trailing newline before the '}'.
        descriptor.Template!.Content.ShouldBe("    <div>{{ message }}</div>\n");
        descriptor.Script!.Content.ShouldBe("    public string Message = \"Hello\";\n");
        descriptor.Styles[0].Content.ShouldBe("    .box { color: red; }\n");
    }

    [Fact]
    public void Parse_Descriptor_KeepsTheFullSource()
    {
        SfcTestHelpers.Parse(Component).Source.ShouldBe(Component);
    }

    [Fact]
    public void Parse_EmptyFile_YieldsAnEmptyDescriptorWithNoErrors()
    {
        var result = SfcParser.Parse(string.Empty);

        result.Descriptor.Template.ShouldBeNull();
        result.Descriptor.Script.ShouldBeNull();
        result.Descriptor.Styles.Count.ShouldBe(0);
        result.Descriptor.CustomBlocks.Count.ShouldBe(0);
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_BlankLinesBetweenBlocks_AreTolerated()
    {
        var source = "@template {\n    <p/>\n}\n\n\n@script {\n    // c#\n}\n";

        var descriptor = SfcTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Script.ShouldNotBeNull();
        SfcTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_EmptyBlockBody_YieldsEmptyContent()
    {
        var descriptor = SfcTestHelpers.Parse("@template {\n}\n");

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_MultipleStyleBlocks_ArePreservedInOrder()
    {
        var source =
            "@style {\n    .a { color: red; }\n}\n" +
            "@style scoped {\n    .b { color: blue; }\n}\n";

        var descriptor = SfcTestHelpers.Parse(source);

        descriptor.Styles.Count.ShouldBe(2);
        descriptor.Styles[0].Scoped.ShouldBeFalse();
        descriptor.Styles[1].Scoped.ShouldBeTrue();
        SfcTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_CustomBlock_IsPreservedNotRejected()
    {
        var source = "@docs {\n    Usage notes.\n}\n";

        var descriptor = SfcTestHelpers.Parse(source);

        descriptor.CustomBlocks.Count.ShouldBe(1);
        descriptor.CustomBlocks[0].Kind.ShouldBe(SfcBlockKind.Custom);
        descriptor.CustomBlocks[0].Name.ShouldBe("docs");
        descriptor.CustomBlocks[0].Content.ShouldBe("    Usage notes.\n");
        SfcTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_FileWithoutTrailingNewline_StillClosesTheBlock()
    {
        var source = "@template {\n    <p/>\n}";

        var descriptor = SfcTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe("    <p/>\n");
        SfcTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_CrlfLineEndings_AreHandledAndPreservedInContent()
    {
        // Real .viu files authored on Windows use CRLF; the parser treats \r\n as one terminator and
        // keeps it verbatim in the raw content slice.
        var source = "@template {\r\n    <x/>\r\n}\r\n";

        var descriptor = SfcTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe("    <x/>\r\n");
        SfcTestHelpers.Errors(source).Count.ShouldBe(0);
    }
}
