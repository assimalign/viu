using System;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Sfc;

// Malformed input produces structured, located diagnostics; the parser recovers and never throws for
// bad content, reporting multiple problems in one pass. These are Vuecs-defined codes (SfcErrorCode) —
// the @-block container has no upstream vuejs/core numbering to mirror.
public class DiagnosticsTests
{
    [Fact]
    public void Parse_UnterminatedBlock_ReportsAndRecoversToEndOfFile()
    {
        var source = "@script {\n    x\n";

        var result = SfcParser.Parse(source);

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.UnterminatedBlock);
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("    x\n");
    }

    [Fact]
    public void Parse_DuplicateTemplate_KeepsFirstAndReports()
    {
        var source =
            "@template {\n    <first/>\n}\n" +
            "@template {\n    <second/>\n}\n";

        var result = SfcParser.Parse(source);

        result.Descriptor.Template!.Content.ShouldBe("    <first/>\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.DuplicateTemplateBlock);
    }

    [Fact]
    public void Parse_DuplicateScript_KeepsFirstAndReports()
    {
        var source =
            "@script {\n    // first\n}\n" +
            "@script {\n    // second\n}\n";

        var result = SfcParser.Parse(source);

        result.Descriptor.Script!.Content.ShouldBe("    // first\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.DuplicateScriptBlock);
    }

    [Fact]
    public void Parse_StrayTopLevelContent_IsReported()
    {
        var source = "oops\n@script {\n}\n";

        var result = SfcParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.StrayTopLevelContent);
    }

    [Fact]
    public void Parse_ContentAfterOpeningBrace_ReportsButStillOpensTheBlock()
    {
        var source = "@script { junk\n}\n";

        var result = SfcParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.ContentAfterOpeningBrace);
    }

    [Fact]
    public void Parse_UnquotedOptionValue_ReportsButStillOpensTheBlock()
    {
        var source = "@style lang=scss {\n}\n";

        var result = SfcParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Lang.ShouldBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.MalformedOptionValue);
    }

    [Fact]
    public void Parse_UnterminatedOptionValue_IsReported()
    {
        var source = "@style lang=\"scss\n}\n";

        SfcTestHelpers.Errors(source).ShouldContain(error => error.Code == SfcErrorCode.MalformedOptionValue);
    }

    [Fact]
    public void Parse_HeaderWithoutName_ReportsMalformedHeader()
    {
        var source = "@ {\n}\n";

        SfcTestHelpers.Errors(source).ShouldContain(error => error.Code == SfcErrorCode.MalformedBlockHeader);
    }

    [Fact]
    public void Parse_HeaderWithoutBrace_ReportsMissingBrace()
    {
        var result = SfcParser.Parse("@script\n");

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SfcErrorCode.MissingOpeningBrace);
        result.Descriptor.Script.ShouldBeNull();
    }

    [Fact]
    public void Parse_SeveralProblems_AreAllReportedInOnePass()
    {
        var source =
            "oops\n" +
            "@template {\n}\n" +
            "@template {\n}\n";

        var errors = SfcTestHelpers.Errors(source);

        errors.Count.ShouldBe(2);
        errors.ShouldContain(error => error.Code == SfcErrorCode.StrayTopLevelContent);
        errors.ShouldContain(error => error.Code == SfcErrorCode.DuplicateTemplateBlock);
    }

    [Fact]
    public void Parse_MessagesAreNonEmptyAndMatchTheCatalog()
    {
        var error = SfcParser.Parse("@script\n").Errors[0];

        error.Message.ShouldNotBeNullOrEmpty();
        error.Message.ShouldBe(SfcErrorMessages.GetMessage(SfcErrorCode.MissingOpeningBrace));
    }

    [Fact]
    public void Parse_MalformedInputs_NeverThrow()
    {
        var inputs = new[]
        {
            "@",
            "@ {",
            "@script",
            "@script {",
            "@style lang=",
            "@style lang=\"x",
            "}",
            "}}}}",
            "@@@@",
            "   ",
            "@template {\n@template {\n",
            "random text with { and } braces",
        };

        foreach (var input in inputs)
        {
            Should.NotThrow(() => { SfcParser.Parse(input); });
        }
    }

    [Fact]
    public void Parse_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() => { SfcParser.Parse(null!); });
    }
}
