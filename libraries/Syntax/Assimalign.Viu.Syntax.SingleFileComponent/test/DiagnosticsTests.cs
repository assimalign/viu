using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// Malformed input produces structured, located diagnostics; the parser recovers and never throws for
// bad content, reporting multiple problems in one pass ([SFC-DIAG-1]). The codes come from the
// container's own 1000-based SingleFileComponentErrorCode catalog, kept distinct from the template
// compiler's CompilerErrorCode so a reader can tell which stage reported a code.
// Legacy-container migration warnings (1015/1016) are pinned in LegacyBlockSyntaxTests; the
// <script>-tag rejection (1017) in ScriptTagDiagnosticTests.
public class DiagnosticsTests
{
    [Fact]
    public void Parse_UnterminatedAtBlock_ReportsAndRecoversToEndOfFile()
    {
        var source = "@script {\n    x\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.UnterminatedBlock);
        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Content.ShouldBe("    x\n");
    }

    [Fact]
    public void Parse_UnterminatedTagBlock_ReportsAndRecoversToEndOfFile()
    {
        var source = "<style scoped>\n.card {\n  display: grid;\n}";

        var result = SingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.UnterminatedTagBlock);
        result.Errors[0].Location.Source.ShouldBe("<style scoped>");
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].Content.ShouldBe("\n.card {\n  display: grid;\n}");
    }

    [Fact]
    public void Parse_DuplicateTemplate_KeepsFirstAndReports()
    {
        var source =
            "<template>\n    <first/>\n</template>\n" +
            "<template>\n    <second/>\n</template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template!.Content.ShouldBe("\n    <first/>\n");
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
    public void Parse_UnexpectedTopLevelClosingTag_IsReported()
    {
        var source = "</template>\n<template></template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.UnexpectedClosingTag);
    }

    [Fact]
    public void Parse_MalformedTag_ReportsAndRecoversToTheNextLine()
    {
        // "<1invalid>" has no valid tag name; the scanner reports 1009 and the parser resumes on the
        // next line, so the following block still parses.
        var source = "<1invalid>\n<template></template>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.MalformedTagBlock);
    }

    [Fact]
    public void Parse_MalformedTagAttribute_ReportsAndRecoversToTheNextLine()
    {
        // "junk=" with no value is a malformed attribute (1010); the tag fails to open and the parser
        // recovers on the next line, keeping the following block.
        var source = "<template junk=>\n<style></style>\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Template.ShouldBeNull();
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.MalformedTagAttribute);
    }

    [Fact]
    public void Parse_UnknownTopLevelTag_IsStrayWithWholeElementRecovery()
    {
        // Custom blocks stay @-syntax; an unknown tag is stray content, and recovery skips the whole
        // element (not each line of it) so a multi-line element reports exactly one diagnostic.
        var source = "<docs>\n    line one\n    line two\n</docs>\n@script {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.CustomBlocks.Count.ShouldBe(0);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.StrayTopLevelContent);
        result.Errors[0].Location.Source.ShouldBe("<docs>");
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
    public void Parse_UnquotedAtOptionValue_ReportsButStillOpensTheBlock()
    {
        // The @-option grammar requires double-quoted values (unlike tag attributes).
        var source = "@script lang=csharp {\n}\n";

        var result = SingleFileComponentParser.Parse(source);

        result.Descriptor.Script.ShouldNotBeNull();
        result.Descriptor.Script!.Lang.ShouldBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Code.ShouldBe(SingleFileComponentErrorCode.MalformedOptionValue);
    }

    [Fact]
    public void Parse_UnterminatedAtOptionValue_IsReported()
    {
        var source = "@script lang=\"csharp\n}\n";

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
            "<template></template>\n" +
            "<template></template>\n";

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
            "@script lang=",
            "@script lang=\"x",
            "}",
            "}}}}",
            "@@@@",
            "   ",
            "@template {\n@template {\n",
            "random text with { and } braces",
            "<",
            "<>",
            "</",
            "</>",
            "<template",
            "<template>",
            "<template lang=\"x",
            "<template junk=>",
            "<style scoped scoped>",
            "<script>",
            "<script",
            "<!--",
            "<!-- unclosed",
            "<1invalid>",
            "<template><style></template>",
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
