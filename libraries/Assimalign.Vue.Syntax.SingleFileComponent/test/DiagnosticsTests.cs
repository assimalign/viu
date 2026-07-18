using System;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

// Malformed input produces structured, located diagnostics; the parser recovers and never throws for
// bad content, reporting multiple problems in one pass. These are Vuecs-defined codes (SingleFileComponentErrorCode) —
// the @-block container has no upstream vuejs/core numbering to mirror.
public class DiagnosticsTests
{
    [Fact]
    public void Parse_UnterminatedBlock_ReportsAndRecoversToEndOfFile()
    {
        var source = "@script {\n    x\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.UnterminatedBlock);
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("    x\n");
    }

    [Fact]
    public void Parse_DuplicateTemplate_KeepsFirstAndReports()
    {
        var source =
            "@template {\n    <first/>\n}\n" +
            "@template {\n    <second/>\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template!.Content.ShouldBe("    <first/>\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.DuplicateTemplateBlock);
    }

    [Fact]
    public void Parse_DuplicateScript_KeepsFirstAndReports()
    {
        var source =
            "@script {\n    // first\n}\n" +
            "@script {\n    // second\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script!.Content.ShouldBe("    // first\n");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.DuplicateScriptBlock);
    }

    [Fact]
    public void Parse_StrayTopLevelContent_IsReported()
    {
        var source = "oops\n@script {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.StrayTopLevelContent);
    }

    [Fact]
    public void Parse_ContentAfterOpeningBrace_ReportsButStillOpensTheBlock()
    {
        var source = "@script { junk\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.ContentAfterOpeningBrace);
    }

    [Fact]
    public void Parse_UnquotedOptionValue_ReportsButStillOpensTheBlock()
    {
        var source = "@style lang=scss {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Lang.ShouldBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.MalformedOptionValue);
    }

    [Fact]
    public void Parse_UnterminatedOptionValue_IsReported()
    {
        var source = "@style lang=\"scss\n}\n";

        SingleFileComponentTestHelpers.Errors(source).ShouldContain(error => error.Code == SingleFileComponentErrorCode.MalformedOptionValue);
    }

    [Fact]
    public void Parse_HeaderWithoutName_ReportsMalformedHeader()
    {
        var source = "@ {\n}\n";

        SingleFileComponentTestHelpers.Errors(source).ShouldContain(error => error.Code == SingleFileComponentErrorCode.MalformedBlockHeader);
    }

    [Fact]
    public void Parse_HeaderWithoutBrace_ReportsMissingBrace()
    {
        var result = SingleFileComponentParser.Parse("@script\n");

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.MissingOpeningBrace);
        result.Descriptor.Script.ShouldBeNull();
    }

    [Fact]
    public void Parse_SeveralProblems_AreAllReportedInOnePass()
    {
        var source =
            "oops\n" +
            "@template {\n}\n" +
            "@template {\n}\n";

        var errors = SingleFileComponentTestHelpers.Errors(source);

        errors.Count.ShouldBe(2);
        errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.StrayTopLevelContent);
        errors.ShouldContain(error => error.Code == SingleFileComponentErrorCode.DuplicateTemplateBlock);
    }

    [Fact]
    public void Parse_MessagesAreNonEmptyAndMatchTheCatalog()
    {
        var error = SingleFileComponentParser.Parse("@script\n").Errors[0];

        error.Message.ShouldNotBeNullOrEmpty();
        error.Message.ShouldBe(SingleFileComponentErrorMessages.GetMessage(SingleFileComponentErrorCode.MissingOpeningBrace));
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
            Should.NotThrow(() => { SingleFileComponentParser.Parse(input); });
        }
    }

    [Fact]
    public void Parse_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() => { SingleFileComponentParser.Parse(null!); });
    }
}
