using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins component-style selector completion in template class values
/// ([V01.01.12.07.13], #334).
/// </summary>
public class StyleClassCompletionTests
{
    private const string DocumentUri = "file:///C:/workspace/App/Card.viu";

    [Fact]
    public void GetCompletions_ClassAttribute_ReturnsMatchingStyleSelectors()
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

        completions.Select(item => item.Label).ShouldBe(
            ["gallery", "gallery-wide", "gap-local"]);
        completions.ShouldAllBe(item => item.Detail == "Component style class");
    }

    [Fact]
    public void GetCompletions_DuplicateStyleSelector_ReturnsItOnce()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"card-shell\"></div>\n" +
            "</template>\n" +
            "<style>\n" +
            "  .card-shell { display: grid; }\n" +
            "  @media (width >= 40rem) { .card-shell { gap: 1rem; } }\n" +
            "</style>\n";

        var completions = CompleteAfter(source, "class=\"card-shell");

        completions.Count(item => item.Label == "card-shell").ShouldBe(1);
    }

    [Fact]
    public void GetCompletions_ClassValueWithoutStyleClasses_ReturnsNoAttributeNameItems()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"missing\"></div>\n" +
            "</template>\n";

        var completions = CompleteAfter(source, "class=\"miss");

        // Regression pin for 7a04698: the class-value path owns the request even when its component
        // style result is empty, so TemplateCompletionProvider cannot answer with attribute names.
        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_BoundClassLiteral_ReturnsMatchingStyleSelector()
    {
        const string source =
            "<template>\n" +
            "  <div :class=\"['card-', Condition ? 'active' : '']\"></div>\n" +
            "</template>\n" +
            "<style>.card-shell { display: block; }</style>\n";

        var completion = CompleteAfter(source, "'card-")
            .Single(item => item.Label == "card-shell");

        completion.EditRange.ShouldNotBeNull();
        completion.FilterText.ShouldBe("card-shell");
    }

    [Fact]
    public void GetCompletions_ComponentStyleClass_LeavesColorTransportDormant()
    {
        const string source =
            "<template><div class=\"brand\"></div></template>\n" +
            "<style>.brand { color: #123456; }</style>\n";

        var completion = CompleteAfter(source, "class=\"brand")
            .Single(item => item.Label == "brand");

        completion.Kind.ShouldBe(LanguageCompletionItemKind.Property);
        completion.ColorValue.ShouldBeNull();
    }

    [Fact]
    public void GetCompletions_DocumentUriCasingDiffersFromOpen_StillResolvesTheOpenDocument()
    {
        const string source =
            "<template><div class=\"card-\"></div></template>\n" +
            "<style>.card-shell { display: block; }</style>\n";
        var service = LanguageServices.Create();
        service.OpenDocument("file:///c:/workspace/Card.viu", source, 1);

        // The server host tracks open documents case-insensitively, so the workspace must agree or
        // a client that varies the drive-letter casing silently loses every language feature.
        var completions = service.GetCompletions(
            "file:///C:/workspace/Card.viu",
            PositionAfter(source, "class=\"card-"));

        completions.ShouldContain(item => item.Label == "card-shell");
    }

    private static IReadOnlyList<LanguageCompletionItem> CompleteAfter(
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
