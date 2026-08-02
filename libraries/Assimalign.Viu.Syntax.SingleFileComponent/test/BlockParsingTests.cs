using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The happy-path block slicing: a well-formed .viu file yields typed template/script/style/custom
// blocks with exact raw content. Block semantics mirror the Vue SFC spec
// (https://vuejs.org/api/sfc-spec.html); the [V01.01.06.10] hybrid container is tag-based
// <template>/<style> plus @script and @-form custom blocks (see docs/FORMAT.md).
public class BlockParsingTests
{
    private const string Component =
        "<template>\n" +
        "    <div>{{ message }}</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "    public string Message = \"Hello\";\n" +
        "}\n" +
        "<style scoped>\n" +
        "    .box { color: red; }\n" +
        "</style>\n";

    [Fact]
    public void Parse_WellFormedComponent_ExposesEachBlock()
    {
        var descriptor = SingleFileComponentTestHelpers.Parse(Component);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Script.ShouldNotBeNull();
        descriptor.Styles.Count.ShouldBe(1);
        descriptor.CustomBlocks.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_WellFormedComponent_ReportsNoErrors()
    {
        SingleFileComponentTestHelpers.Errors(Component).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_Blocks_CarryTheirKindAndName()
    {
        var descriptor = SingleFileComponentTestHelpers.Parse(Component);

        descriptor.Template!.Kind.ShouldBe(SingleFileComponentBlockKind.Template);
        descriptor.Template!.Name.ShouldBe("template");
        descriptor.Script!.Kind.ShouldBe(SingleFileComponentBlockKind.Script);
        descriptor.Script!.Name.ShouldBe("script");
        descriptor.Styles[0].Kind.ShouldBe(SingleFileComponentBlockKind.Style);
        descriptor.Styles[0].Name.ShouldBe("style");
    }

    [Fact]
    public void Parse_Content_IsTheExactRawSliceForEachContainer()
    {
        var descriptor = SingleFileComponentTestHelpers.Parse(Component);

        // Tag-block content runs from just past the opening tag's '>' to the '<' of the closing tag, so
        // it keeps the leading newline (matching .vue); @-block content runs from the line after the
        // header to the closing-brace line, so it has no leading newline. Both keep interior
        // indentation and the trailing newline verbatim.
        descriptor.Template!.Content.ShouldBe("\n    <div>{{ message }}</div>\n");
        descriptor.Script!.Content.ShouldBe("    public string Message = \"Hello\";\n");
        descriptor.Styles[0].Content.ShouldBe("\n    .box { color: red; }\n");
    }

    [Fact]
    public void Parse_Descriptor_KeepsTheFullSource()
    {
        SingleFileComponentTestHelpers.Parse(Component).Source.ShouldBe(Component);
    }

    [Fact]
    public void Parse_EmptyFile_YieldsAnEmptyDescriptorWithNoErrors()
    {
        var result = SingleFileComponentParser.Parse(string.Empty);

        result.Descriptor.Template.ShouldBeNull();
        result.Descriptor.Script.ShouldBeNull();
        result.Descriptor.Styles.Count.ShouldBe(0);
        result.Descriptor.CustomBlocks.Count.ShouldBe(0);
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_BlankLinesBetweenBlocks_AreTolerated()
    {
        var source = "<template>\n    <p/>\n</template>\n\n\n@script {\n    // c#\n}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Script.ShouldNotBeNull();
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_EmptyBlockBodies_YieldEmptyContent()
    {
        var descriptor = SingleFileComponentTestHelpers.Parse("<template></template>\n@script {\n}\n");

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe(string.Empty);
        descriptor.Script.ShouldNotBeNull();
        descriptor.Script!.Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_MultipleStyleBlocks_ArePreservedInOrder()
    {
        var source =
            "<style>\n    .a { color: red; }\n</style>\n" +
            "<style scoped>\n    .b { color: blue; }\n</style>\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Styles.Count.ShouldBe(2);
        descriptor.Styles[0].Scoped.ShouldBeFalse();
        descriptor.Styles[1].Scoped.ShouldBeTrue();
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_CustomBlock_IsPreservedNotRejected()
    {
        // Custom blocks stay @-syntax under the [V01.01.06.10] hybrid container.
        var source = "@docs {\n    Usage notes.\n}\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.CustomBlocks.Count.ShouldBe(1);
        descriptor.CustomBlocks[0].Kind.ShouldBe(SingleFileComponentBlockKind.Custom);
        descriptor.CustomBlocks[0].Name.ShouldBe("docs");
        descriptor.CustomBlocks[0].Content.ShouldBe("    Usage notes.\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_FileWithoutTrailingNewline_StillClosesTheBlocks()
    {
        var tagSource = "<template>\n    <p/>\n</template>";
        var atSource = "@script {\n    x();\n}";

        var tagDescriptor = SingleFileComponentTestHelpers.Parse(tagSource);
        var atDescriptor = SingleFileComponentTestHelpers.Parse(atSource);

        tagDescriptor.Template.ShouldNotBeNull();
        tagDescriptor.Template!.Content.ShouldBe("\n    <p/>\n");
        SingleFileComponentTestHelpers.Errors(tagSource).Count.ShouldBe(0);
        atDescriptor.Script.ShouldNotBeNull();
        atDescriptor.Script!.Content.ShouldBe("    x();\n");
        SingleFileComponentTestHelpers.Errors(atSource).Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_CrlfLineEndings_AreHandledAndPreservedInContent()
    {
        // Real .viu files authored on Windows use CRLF; both containers treat \r\n as one terminator and
        // keep it verbatim in the raw content slice.
        var source = "<template>\r\n    <x/>\r\n</template>\r\n@script {\r\n    y();\r\n}\r\n";

        var descriptor = SingleFileComponentTestHelpers.Parse(source);

        descriptor.Template.ShouldNotBeNull();
        descriptor.Template!.Content.ShouldBe("\r\n    <x/>\r\n");
        descriptor.Script!.Content.ShouldBe("    y();\r\n");
        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }
}
