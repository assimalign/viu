using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

// The [V01.01.06.01] span contract: every block exposes exact start/end line-column-offset spans for
// both the whole block and its content region (suitable for #line mapping and IDE diagnostics), and
// every span's Source equals the exact source slice between its offsets.
public class LocationTests
{
    [Fact]
    public void Parse_Block_HasExactWholeAndContentSpans()
    {
        // "@template {\n    <div/>\n}\n"
        //  offsets: header line [0,11], '\n' at 11; content line [12,21], '\n' at 22; '}' at 23.
        var descriptor = SingleFileComponentTestHelpers.Parse("@template {\n    <div/>\n}\n");
        var template = descriptor.Template!;

        template.Location.Start.ShouldBe(new Position(0, 1, 1));
        template.Location.End.ShouldBe(new Position(24, 3, 2));
        template.Location.Source.ShouldBe("@template {\n    <div/>\n}");

        template.ContentLocation.Start.ShouldBe(new Position(12, 2, 1));
        template.ContentLocation.End.ShouldBe(new Position(23, 3, 1));
        template.Content.ShouldBe("    <div/>\n");
    }

    [Fact]
    public void Parse_BlockStartingOnLaterLine_TracksLineAndColumn()
    {
        // The @script header begins at offset 14, which is line 3, column 1.
        var descriptor = SingleFileComponentTestHelpers.Parse("@template {\n}\n@script {\n    x\n}\n");

        descriptor.Template!.Content.ShouldBe(string.Empty);
        descriptor.Template!.ContentLocation.Start.ShouldBe(new Position(12, 2, 1));

        descriptor.Script!.Location.Start.ShouldBe(new Position(14, 3, 1));
        descriptor.Script!.Content.ShouldBe("    x\n");
    }

    [Fact]
    public void Parse_Option_HasATokenSpan()
    {
        // "@style scoped {\n}\n": the "scoped" token is offsets [7,13).
        var style = SingleFileComponentTestHelpers.Parse("@style scoped {\n}\n").Styles[0];
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
            "@template {\n    <div>{{ x }}</div>\n}\n" +
            "@script lang=\"csharp\" {\n    var y = \"}\";\n}\n" +
            "@style scoped {\n    .a { color: red; }\n}\n");

        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void Parse_MalformedInput_DiagnosticSpansAreExact()
    {
        var result = SingleFileComponentParser.Parse("oops\n@style lang=scss {\n}\n@template {\n");

        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }
}
