using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The [V01.01.06.10] <script>-tag rule: a .viu component's C# lives in @script { } only. A top-level
// <script ...> tag reports ScriptTagBlockNotSupported (1017, Error severity) at its opening tag and
// contributes no block — its content must never reach compilation. Recovery slices past the element.
public class ScriptTagDiagnosticTests
{
    [Fact]
    public void Parse_ScriptTag_ReportsNotSupportedAndContributesNoBlock()
    {
        var source = "<script>\n    var x = 1;\n</script>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script.ShouldBeNull();
        result.Descriptor.CustomBlocks.Count.ShouldBe(0);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.ScriptTagBlockNotSupported);
        result.Errors[0].Severity.ShouldBe(DiagnosticSeverity.Error);
        result.Errors[0].Location.Source.ShouldBe("<script>");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_ScriptTag_MessagePointsAtAtScript()
    {
        var error = SingleFileComponentParser.Parse("<script>x</script>\n").Errors[0];

        error.Message.ShouldBe(SingleFileComponentErrorMessages.GetMessage(SingleFileComponentErrorCode.ScriptTagBlockNotSupported));
        error.Message.ShouldContain("@script { }");
    }

    [Fact]
    public void Parse_ScriptTag_ResumesPastTheClosingTag()
    {
        // The element is sliced past; the surrounding canonical blocks still parse.
        var source =
            "<template>\n    <div/>\n</template>\n" +
            "<script setup lang=\"csharp\">\n    public int X;\n</script>\n" +
            "@script {\n    public int Y;\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldNotBeNull();
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("    public int Y;\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.ScriptTagBlockNotSupported);
    }

    [Fact]
    public void Parse_SelfClosingScriptTag_StillReports()
    {
        var result = SingleFileComponentParser.Parse("<script />\n");

        result.Descriptor.Script.ShouldBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.ScriptTagBlockNotSupported);
    }

    [Fact]
    public void Parse_UnterminatedScriptTag_ConsumesToEndOfFileWithoutABlock()
    {
        var result = SingleFileComponentParser.Parse("<script>\n    var x = 1;\n");

        result.Descriptor.Script.ShouldBeNull();
        result.Descriptor.CustomBlocks.Count.ShouldBe(0);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.ScriptTagBlockNotSupported);
    }
}
