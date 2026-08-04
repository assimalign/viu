using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins block-content folding: a fold covers the block content while the closing delimiter line
/// stays visible, and single-line blocks fold nothing.
/// </summary>
public class FoldingRangeTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";

    [Fact]
    public void GetFoldingRanges_BlockContent_KeepsClosingDelimiterVisible()
    {
        const string source =
            "<template>\n" +
            "    <div />\n" +
            "    <span />\n" +
            "</template>\n" +
            "@script {\n" +
            "    public int Count;\n" +
            "}\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 2));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 4, EndLine: 5));
    }

    [Fact]
    public void GetFoldingRanges_SingleLineBlock_ReturnsNoRange()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "<template><div /></template>\n", 1);

        service.GetFoldingRanges(DocumentUri).ShouldBeEmpty();
    }
}
