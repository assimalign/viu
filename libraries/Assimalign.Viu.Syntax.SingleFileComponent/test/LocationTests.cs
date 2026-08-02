using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// The [V01.01.06.01] span contract, upheld across the [V01.01.06.10] hybrid container: every block
// exposes exact start/end line-column-offset spans for both the whole block and its content region
// (suitable for #line mapping and IDE diagnostics), and every span's Source equals the exact source
// slice between its offsets.
public class LocationTests
{
    [Fact]
    public void Parse_TagBlock_HasExactWholeAndContentSpans()
    {
        // "<template>\n    <div/>\n</template>\n"
        //  offsets: opening tag [0,10); '\n' at 10; content line [11,21), '\n' at 21; closing tag [22,33).
        var descriptor = SingleFileComponentTestHelpers.Parse("<template>\n    <div/>\n</template>\n");
        var template = descriptor.Template!;

        template.Location.Start.ShouldBe(new Position(0, 1, 1));
        template.Location.End.ShouldBe(new Position(33, 3, 12));
        template.Location.Source.ShouldBe("<template>\n    <div/>\n</template>");

        // Tag-block content is the exact slice between the opening tag's '>' and the closing tag's '<'
        // — it starts on the header line itself, matching .vue semantics.
        template.ContentLocation.Start.ShouldBe(new Position(10, 1, 11));
        template.ContentLocation.End.ShouldBe(new Position(22, 3, 1));
        template.Content.ShouldBe("\n    <div/>\n");
    }

    [Fact]
    public void Parse_InlineSameLineTagContent_HasExactSpans()
    {
        // "<template><div/></template>\n" — the whole block on one line; content is the inner slice.
        var result = SingleFileComponentParser.Parse("<template><div/></template>\n");
        var template = result.Descriptor.Template!;

        template.Location.Start.ShouldBe(new Position(0, 1, 1));
        template.Location.End.ShouldBe(new Position(27, 1, 28));
        template.ContentLocation.Start.ShouldBe(new Position(10, 1, 11));
        template.ContentLocation.End.ShouldBe(new Position(16, 1, 17));
        template.Content.ShouldBe("<div/>");
        result.Errors.Count.ShouldBe(0);
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_BlockStartingOnLaterLine_TracksLineAndColumn()
    {
        // The @script header begins at offset 22, which is line 2, column 1.
        var descriptor = SingleFileComponentTestHelpers.Parse("<template></template>\n@script {\n    x\n}\n");

        descriptor.Template!.Content.ShouldBe(string.Empty);
        descriptor.Template!.ContentLocation.Start.ShouldBe(new Position(10, 1, 11));

        descriptor.Script!.Location.Start.ShouldBe(new Position(22, 2, 1));
        descriptor.Script!.Content.ShouldBe("    x\n");
    }

    [Fact]
    public void Parse_TagAttribute_HasATokenSpan()
    {
        // "<style scoped>\n</style>\n": the "scoped" token is offsets [7,13).
        var style = SingleFileComponentTestHelpers.Parse("<style scoped>\n</style>\n").Styles[0];
        var scoped = style.Options[0];

        scoped.Name.ShouldBe("scoped");
        scoped.Location.Source.ShouldBe("scoped");
        scoped.Location.Start.ShouldBe(new Position(7, 1, 8));
        scoped.Location.End.ShouldBe(new Position(13, 1, 14));
    }

    [Fact]
    public void Parse_WellFormedComponent_AllSpansAreExact()
    {
        var result = SingleFileComponentParser.Parse(
            "<template>\n    <div>{{ x }}</div>\n</template>\n" +
            "@script lang=\"csharp\" {\n    var y = \"}\";\n}\n" +
            "<style scoped>\n    .a { color: red; }\n</style>\n");

        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_MalformedInput_DiagnosticSpansAreExact()
    {
        // Stray content, a legacy @style header with a malformed option, and an unterminated tag block —
        // every diagnostic (errors and warnings alike) still carries an exact span.
        var result = SingleFileComponentParser.Parse("oops\n@style lang=scss {\n}\n<template>\n");

        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }
}
