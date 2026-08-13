using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The [V01.01.06.10] legacy transition window: the pre-pivot @template/@style containers still parse
// with unchanged slicing and option grammar, but each header reports a Warning-severity migration
// diagnostic (LegacyTemplateBlockSyntax 1015 / LegacyStyleBlockSyntax 1016) pointing at the canonical
// <template>/<style> tags. @script and custom @-blocks are canonical and report nothing.
public class LegacyBlockSyntaxTests
{
    [Fact]
    public void Parse_LegacyTemplateBlock_ReportsMigrationWarningAndStillSlices()
    {
        var source = "@template {\n    <div/>\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Template!.Content.ShouldBe("    <div/>\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.LegacyTemplateBlockSyntax);
        result.Errors[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Parse_LegacyStyleBlock_ReportsMigrationWarningAndStillSlices()
    {
        var source = "@style scoped {\n    .a { color: red; }\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Scoped.ShouldBeTrue();
        result.Descriptor.Styles[0].Content.ShouldBe("    .a { color: red; }\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.LegacyStyleBlockSyntax);
        result.Errors[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Parse_LegacyStyleBlock_KeepsTheAtOptionGrammar()
    {
        var source = "@style module=\"classes\" lang=\"scss\" {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        var style = result.Descriptor.Styles[0];
        style.IsModule.ShouldBeTrue();
        style.ModuleName.ShouldBe("classes");
        style.Lang.ShouldBe("scss");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.LegacyStyleBlockSyntax);
    }

    [Fact]
    public void Parse_LegacyWarning_PointsAtTheHeaderLine()
    {
        var result = SingleFileComponentParser.Parse("@template lang=\"html\" {\n    <p/>\n}\n");

        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.LegacyTemplateBlockSyntax);
        result.Errors[0].Location.Source.ShouldBe("@template lang=\"html\" {");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_LegacyWarningMessages_MatchTheCatalogAndNameTheCanonicalTags()
    {
        var template = SingleFileComponentParser.Parse("@template {\n}\n").Errors[0];
        var style = SingleFileComponentParser.Parse("@style {\n}\n").Errors[0];

        template.Message.ShouldBe(SingleFileComponentErrorMessages.GetMessage(SingleFileComponentErrorCode.LegacyTemplateBlockSyntax));
        template.Message.ShouldContain("<template>");
        style.Message.ShouldBe(SingleFileComponentErrorMessages.GetMessage(SingleFileComponentErrorCode.LegacyStyleBlockSyntax));
        style.Message.ShouldContain("<style>");
    }

    [Fact]
    public void Parse_FullyLegacyComponent_SlicesEveryBlockAndWarnsPerLegacyHeader()
    {
        var source =
            "@template {\n    <div>{{ x }}</div>\n}\n" +
            "@script {\n    public int X;\n}\n" +
            "@style {\n    .a { color: red; }\n}\n" +
            "@style scoped {\n    .b { color: blue; }\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Styles.Count.ShouldBe(2);
        result.Errors.Count.ShouldBe(3);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.LegacyTemplateBlockSyntax);
        result.Errors[1].Code.ShouldBe(SingleFileComponentErrorCode.LegacyStyleBlockSyntax);
        result.Errors[2].Code.ShouldBe(SingleFileComponentErrorCode.LegacyStyleBlockSyntax);
        foreach (var error in result.Errors)
        {
            error.Severity.ShouldBe(DiagnosticSeverity.Warning);
        }

        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_CanonicalAtBlocks_ReportNoLegacyWarning()
    {
        // @script and custom @-blocks are the canonical @-forms — only @template/@style are legacy.
        var source = "@script {\n    x();\n}\n@docs {\n    notes\n}\n";

        SingleFileComponentTestHelpers.Errors(source).Count.ShouldBe(0);
    }
}
