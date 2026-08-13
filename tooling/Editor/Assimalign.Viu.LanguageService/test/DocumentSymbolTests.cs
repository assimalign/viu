using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the hierarchical block outline over the hybrid <c>.viu</c> container ([V01.01.06.10]):
/// tag blocks and the <c>@script</c> block surface as container symbols whose selection is the
/// header name token, contained in the full block range per the Language Server Protocol.
/// </summary>
public class DocumentSymbolTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";
    private const string VueDocumentUri = "file:///workspace/Counter.vue";

    [Fact]
    public void GetDocumentSymbols_ViuBlocks_ReturnsBlockContainersWithHeaderSelection()
    {
        const string source =
            "<template>\n" +
            "    <div />\n" +
            "</template>\n" +
            "@script {\n" +
            "    public int Count;\n" +
            "}\n" +
            "<style scoped>\n" +
            ".card { }\n" +
            "</style>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var symbols = service.GetDocumentSymbols(DocumentUri);

        symbols.Count.ShouldBe(3);
        var template = symbols[0];
        template.Name.ShouldBe("<template>");
        template.Kind.ShouldBe(LanguageSymbolKind.Module);
        template.Detail.ShouldBeNull();
        template.Range.Start.ShouldBe(new LanguagePosition(0, 0));
        template.SelectionRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(0, 1),
                new LanguagePosition(0, 1 + "template".Length)));
        template.Children.ShouldBeEmpty();

        var script = symbols[1];
        script.Name.ShouldBe("@script");
        script.Kind.ShouldBe(LanguageSymbolKind.Class);
        script.Range.Start.ShouldBe(new LanguagePosition(3, 0));
        script.SelectionRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(3, 0),
                new LanguagePosition(3, "@script".Length)));

        var style = symbols[2];
        style.Name.ShouldBe("<style>");
        style.Kind.ShouldBe(LanguageSymbolKind.Module);
        style.Detail.ShouldBe("scoped");
        style.SelectionRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(6, 1),
                new LanguagePosition(6, 1 + "style".Length)));

        // The selection must sit inside the full range (protocol invariant).
        foreach (var symbol in symbols)
        {
            symbol.SelectionRange.Start.Line.ShouldBeGreaterThanOrEqualTo(
                symbol.Range.Start.Line);
            symbol.SelectionRange.End.Line.ShouldBeLessThanOrEqualTo(symbol.Range.End.Line);
        }
    }

    [Fact]
    public void GetDocumentSymbols_VueScriptSetup_UsesTagNameAndSetupSlot()
    {
        const string source =
            "<script setup lang=\"csharp\">\n" +
            "public int Count;\n" +
            "</script>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var symbols = service.GetDocumentSymbols(VueDocumentUri);

        var script = symbols.ShouldHaveSingleItem();
        script.Name.ShouldBe("<script setup>");
        script.Kind.ShouldBe(LanguageSymbolKind.Class);
        script.Detail.ShouldBe("lang=\"csharp\"");
        script.SelectionRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(0, 1),
                new LanguagePosition(0, 1 + "script".Length)));
    }

    [Fact]
    public void GetDocumentSymbols_UnopenedDocument_ReturnsEmpty()
    {
        var service = LanguageServices.Create();

        service.GetDocumentSymbols(DocumentUri).ShouldBeEmpty();
    }
}
