using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The [V01.01.06.10] hybrid container mixes freely: tag <template>/<style> blocks, the @script block,
// custom @-blocks, and (with warnings) legacy @template/@style all coexist in one file. The descriptor
// rules are container-agnostic — at most one template and one script regardless of syntax, so a tag
// <template> and a legacy @template still collide on DuplicateTemplateBlock (1006, first wins).
public class MixedContainerTests
{
    [Fact]
    public void Parse_MixedContainers_SliceEveryBlockWithoutErrors()
    {
        var source =
            "<template>\n    <div>{{ x }}</div>\n</template>\n" +
            "@script {\n    public int X;\n}\n" +
            "<style scoped>\n    .a { color: red; }\n</style>\n" +
            "@docs {\n    notes\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(0);
        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe("\n    <div>{{ x }}</div>\n");
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("    public int X;\n");
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Scoped.ShouldBeTrue();
        result.Descriptor.CustomBlocks.Count.ShouldBe(1);
        result.Descriptor.CustomBlocks[0].Name.ShouldBe("docs");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_TagTemplateThenLegacyTemplate_ReportsDuplicateAndKeepsTheTagBlock()
    {
        var source =
            "<template>\n    <first/>\n</template>\n" +
            "@template {\n    <second/>\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template!.Content.ShouldBe("\n    <first/>\n");
        result.Errors.Count.ShouldBe(2);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.LegacyTemplateBlockSyntax).ShouldBe(1);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.DuplicateTemplateBlock).ShouldBe(1);
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_LegacyTemplateThenTagTemplate_ReportsDuplicateAndKeepsTheLegacyBlock()
    {
        // First wins regardless of container syntax.
        var source =
            "@template {\n    <first/>\n}\n" +
            "<template>\n    <second/>\n</template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template!.Content.ShouldBe("    <first/>\n");
        result.Errors.Count.ShouldBe(2);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.LegacyTemplateBlockSyntax).ShouldBe(1);
        result.Errors.Count(error => error.Code == SingleFileComponentErrorCode.DuplicateTemplateBlock).ShouldBe(1);
    }

    [Fact]
    public void Parse_StylesAcrossSyntaxes_AreAllKeptInSourceOrder()
    {
        // Styles are repeatable, so tag and legacy style blocks accumulate; only the legacy header warns.
        var source =
            "<style>\n    .a { color: red; }\n</style>\n" +
            "@style scoped {\n    .b { color: blue; }\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(2);
        result.Descriptor.Styles[0].Scoped.ShouldBeFalse();
        result.Descriptor.Styles[1].Scoped.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.LegacyStyleBlockSyntax);
        result.Errors[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }
}
