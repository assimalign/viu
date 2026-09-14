using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

// [V01.01.06.17] removes scoped CSS while preserving exact option tokens for tooling.
public class ScopedStyleDiagnosticsTests
{
    private const string Message = "Scoped styles are not supported; Viu compiles component styles as ordinary global stylesheets. Remove the scoped option or use a CSS module.";

    [Theory]
    [InlineData("<style scoped>.card { color: red; }</style>", 7)]
    [InlineData("<style scoped />", 7)]
    [InlineData("@style scoped {\n    .card { color: red; }\n}\n", 7)]
    public void Parse_ScopedStyle_ReportsLocatedErrorAndPreservesOption(string source, int optionOffset)
    {
        var result = SingleFileComponentParser.Parse(source);

        var diagnostic = result.Errors.Single(error => error.Code == SingleFileComponentErrorCode.ScopedStyleNotSupported);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldBe(Message);
        diagnostic.Location.Source.ShouldBe("scoped");
        diagnostic.Location.Start.ShouldBe(new Position(optionOffset, 1, optionOffset + 1));
        diagnostic.Location.End.ShouldBe(new Position(optionOffset + 6, 1, optionOffset + 7));
        result.Descriptor.Styles.Count.ShouldBe(1);
        result.Descriptor.Styles[0].HasOption("scoped").ShouldBeTrue();
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Fact]
    public void ParseVue_ScopedStyle_ReportsLocatedWarningAndPreservesOrdinaryCss()
    {
        // [VUE-2] compatibility input keeps its descriptor; the option no longer rewrites CSS.
        const string source = "<template><div /></template>\n<style scoped>.card { color: red; }</style>";

        var result = VueSingleFileComponentParser.Parse(source);

        result.Errors.Count.ShouldBe(1);
        var diagnostic = result.Errors[0];
        diagnostic.Code.ShouldBe(SingleFileComponentErrorCode.VueScopedStyleNotSupported);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldBe(Message);
        diagnostic.Location.Source.ShouldBe("scoped");
        diagnostic.Location.Start.Line.ShouldBe(2);
        diagnostic.Location.Start.Column.ShouldBe(8);
        var style = result.Descriptor.Styles.Single();
        style.HasOption("scoped").ShouldBeTrue();
        style.IsModule.ShouldBeFalse();
        style.Content.ShouldBe(".card { color: red; }");
        SingleFileComponentTestHelpers.AssertAllSpansExact(result);
    }

    [Theory]
    [InlineData("<style>.card { color: red; }</style>")]
    [InlineData("<style module>.card { color: red; }</style>")]
    public void Parse_SupportedStyle_ReportsNoDiagnostics(string source)
    {
        SingleFileComponentParser.Parse(source).Errors.ShouldBeEmpty();
        VueSingleFileComponentParser.Parse(source).Errors.ShouldBeEmpty();
    }
}
