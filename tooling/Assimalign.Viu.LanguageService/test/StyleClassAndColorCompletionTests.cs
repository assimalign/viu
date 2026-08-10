using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins component-style selector merging and utility color payloads
/// ([V01.01.12.07.13], #334).
/// </summary>
public class StyleClassAndColorCompletionTests
{
    private const string DocumentUri = "file:///C:/workspace/App/Card.viu";

    [Fact]
    public void GetCompletions_ClassAttribute_MergesStyleSelectorsAndUtilities()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"ga\"></div>\n" +
            "</template>\n" +
            "<style scoped>\n" +
            "  .gallery, .gap-local:hover { display: grid; }\n" +
            "  @media (width >= 40rem) { .gallery-wide { gap: 1rem; } }\n" +
            "</style>\n";

        var completions = CompleteAfter(source, "class=\"ga");

        completions.ShouldContain(
            item => item.Label == "gallery" &&
                    item.Detail == "Component style class");
        completions.ShouldContain(item => item.Label == "gallery-wide");
        completions.ShouldContain(item => item.Label == "gap-local");
        completions.ShouldContain(
            item => item.Label == "gap-4" &&
                    item.Detail == "Viu utility class");
    }

    [Fact]
    public void GetCompletions_StyleSelectorCollidesWithUtility_UsesComponentSelectorOnce()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"gap-4\"></div>\n" +
            "</template>\n" +
            "<style>\n" +
            "  .gap-4 { gap: 2rem; }\n" +
            "</style>\n";

        var completions = CompleteAfter(source, "class=\"gap-4");
        var collision = completions.Where(item => item.Label == "gap-4").ToArray();

        collision.Length.ShouldBe(1);
        collision[0].Detail.ShouldBe("Component style class");
    }

    [Fact]
    public void GetCompletions_ColorUtility_CarriesComputedThemeColor()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"bg-blue-\"></div>\n" +
            "</template>\n";

        var completion = CompleteAfter(source, "bg-blue-")
            .Single(item => item.Label == "bg-blue-500");

        completion.Kind.ShouldBe(LanguageCompletionItemKind.Color);
        completion.ColorValue.ShouldBe("oklch(62.3% 0.214 259.815)");
    }

    [Fact]
    public void GetCompletions_NonColorUtility_HasNoColorPayload()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"gap-\"></div>\n" +
            "</template>\n";

        var completion = CompleteAfter(source, "gap-")
            .Single(item => item.Label == "gap-4");

        completion.ColorValue.ShouldBeNull();
    }

    [Fact]
    public void GetCompletions_ProjectThemeColor_CarriesAuthoredComputedValue()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"bg-brand\"></div>\n" +
            "</template>\n";
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IUtilityCssLanguageService>()
            .ConfigureUtilityStylesheet(
                DocumentUri,
                "@theme { --color-brand: #123456; }");
        service.OpenDocument(DocumentUri, source, 1);

        var completion = service.GetCompletions(
                DocumentUri,
                PositionAfter(source, "bg-brand"))
            .Single(item => item.Label == "bg-brand");

        completion.ColorValue.ShouldBe("#123456");
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageCompletionItem> CompleteAfter(
        string source,
        string marker)
    {
        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);
        return service.GetCompletions(DocumentUri, PositionAfter(source, marker));
    }

    private static LanguagePosition PositionAfter(string source, string marker)
    {
        var markerOffset = source.IndexOf(marker, StringComparison.Ordinal);
        markerOffset.ShouldBeGreaterThanOrEqualTo(0);
        return TextCoordinateConverter.GetPosition(source, markerOffset + marker.Length);
    }
}
