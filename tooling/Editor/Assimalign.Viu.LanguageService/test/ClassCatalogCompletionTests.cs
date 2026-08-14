using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins generic build-contributed class-catalog completion, precedence, truncation, color, hover,
/// and snapshot caching ([V01.01.12.30], #346).
/// </summary>
public class ClassCatalogCompletionTests
{
    private const string DocumentUri = "file:///C:/workspace/App/Card.viu";

    [Fact]
    public void GetCompletionList_ClassAttribute_MergesAfterComponentClassesAndDropsCollision()
    {
        const string source =
            "<template><div class=\"\"></div></template>\n" +
            "<style>.card-local { display: block; } .shared { color: red; }</style>\n";
        var service = CreateService(
            source,
            CreateCatalog(
                false,
                new CatalogEntry("shared", ".shared { color: blue; }"),
                new CatalogEntry(
                    "catalog-only",
                    ".catalog-only { display: grid; }",
                    SortText: "0001")));

        var completionList = service.GetCompletionList(
            DocumentUri,
            PositionAfter(source, "class=\""));

        completionList.Items.Select(item => item.Label).ShouldBe(
            ["card-local", "shared", "catalog-only"]);
        completionList.Items.Count(item => item.Label == "shared").ShouldBe(1);
        completionList.Items.Single(item => item.Label == "shared").Detail
            .ShouldBe("Component style class");
        completionList.Items.Single(item => item.Label == "catalog-only").SortText
            .ShouldStartWith("10000:class-catalog:0001:");
        completionList.IsIncomplete.ShouldBeFalse();
    }

    [Fact]
    public void GetCompletionList_ColorEntry_CarriesColorKindAndValue()
    {
        const string source =
            "<template><div class=\"brand\"></div></template>\n";
        var service = CreateService(
            source,
            CreateCatalog(
                false,
                new CatalogEntry(
                    "brand-surface",
                    ".brand-surface { background: #123456; }",
                    ColorValue: "#123456")));

        var completion = service.GetCompletionList(
                DocumentUri,
                PositionAfter(source, "class=\"brand"))
            .Items
            .Single(item => item.Label == "brand-surface");

        completion.Kind.ShouldBe(LanguageCompletionItemKind.Color);
        completion.ColorValue.ShouldBe("#123456");
    }

    [Fact]
    public void GetCompletionList_ComponentAndCatalog_ApplyMaximumOnceAcrossMergedSet()
    {
        const string source =
            "<template><div class=\"\"></div></template>\n" +
            "<style>.authored { display: block; }</style>\n";
        var catalogEntries = Enumerable
            .Range(0, LanguageCompletionLimits.MaximumItems)
            .Select(
                index => new CatalogEntry(
                    $"catalog-{index:D3}",
                    $".catalog-{index:D3} {{ order: {index}; }}"))
            .ToArray();
        var service = CreateService(source, CreateCatalog(false, catalogEntries));

        var completionList = service.GetCompletionList(
            DocumentUri,
            PositionAfter(source, "class=\""));

        completionList.Items.Count.ShouldBe(LanguageCompletionLimits.MaximumItems);
        completionList.Items[0].Label.ShouldBe("authored");
        completionList.Items.Count(item => item.Detail == "Build-contributed class")
            .ShouldBe(LanguageCompletionLimits.MaximumItems - 1);
        completionList.IsIncomplete.ShouldBeTrue();
    }

    [Fact]
    public void GetCompletionList_TruncatedCatalogWithSmallMatch_PropagatesIncomplete()
    {
        const string source =
            "<template><div class=\"rare-\"></div></template>\n";
        var service = CreateService(
            source,
            CreateCatalog(
                true,
                new CatalogEntry("rare-match", ".rare-match { display: block; }")));

        var completionList = service.GetCompletionList(
            DocumentUri,
            PositionAfter(source, "class=\"rare-"));

        completionList.Items.Single().Label.ShouldBe("rare-match");
        completionList.IsIncomplete.ShouldBeTrue();
    }

    [Fact]
    public void GetHover_ComponentClassWinsThenCatalogProvidesFencedCssFallback()
    {
        const string templateLine =
            "  <div class=\"shared catalog-only\"></div>";
        const string componentCss = ".shared { color: red; }";
        const string catalogCss = ".catalog-only { display: grid; }";
        var source =
            $"<template>\n{templateLine}\n</template>\n" +
            $"<style>{componentCss}</style>\n";
        var service = CreateService(
            source,
            CreateCatalog(
                false,
                new CatalogEntry("shared", ".shared { color: blue; }"),
                new CatalogEntry("catalog-only", catalogCss)));
        var sharedStart = templateLine.IndexOf("shared", StringComparison.Ordinal);
        var catalogStart = templateLine.IndexOf("catalog-only", StringComparison.Ordinal);

        var componentHover = service.GetHover(
            DocumentUri,
            new LanguagePosition(1, sharedStart + 2));
        var catalogHover = service.GetHover(
            DocumentUri,
            new LanguagePosition(1, catalogStart + 2));

        componentHover.ShouldNotBeNull();
        componentHover!.Markdown.ShouldContain("declared in this component's style block");
        componentHover.Markdown.ShouldContain(componentCss);
        componentHover.Markdown.ShouldNotContain("color: blue");
        catalogHover.ShouldNotBeNull();
        catalogHover!.Markdown.ShouldBe($"```css\n{catalogCss}\n```");
        catalogHover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, catalogStart),
                new LanguagePosition(1, catalogStart + "catalog-only".Length)));
    }

    [Fact]
    public void ConfigureClassCatalogs_ReferenceIdentityReusesAndReplacesLoadedSnapshot()
    {
        const string source =
            "<template><div class=\"catalog-\"></div></template>\n";
        var json = CreateCatalog(
            false,
            new CatalogEntry("catalog-entry", ".catalog-entry { display: block; }"));
        var firstConfiguration = new LanguageClassCatalogConfiguration([json]);
        var service = (ViuLanguageService)LanguageServices.Create();
        var catalogService = (IClassCatalogLanguageService)service;
        catalogService.ConfigureClassCatalogs(DocumentUri, firstConfiguration);
        service.OpenDocument(DocumentUri, source, 1);
        var position = PositionAfter(source, "class=\"catalog-");

        service.GetCompletionList(DocumentUri, position);
        service.GetCompletionList(DocumentUri, position);
        catalogService.ConfigureClassCatalogs(DocumentUri, firstConfiguration);
        service.GetCompletionList(DocumentUri, position);

        service.ClassCatalogLoadCount.ShouldBe(1);

        catalogService.ConfigureClassCatalogs(
            DocumentUri,
            new LanguageClassCatalogConfiguration([json]));
        service.GetCompletionList(DocumentUri, position);
        service.ClassCatalogLoadCount.ShouldBe(2);

        service.CloseDocument(DocumentUri).ShouldBeTrue();
        service.OpenDocument(DocumentUri, source, 2);
        service.GetCompletionList(DocumentUri, position).Items.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletionList_MalformedCatalogVersion_IgnoresOnlyMalformedCatalog()
    {
        const string source =
            "<template><div class=\"catalog-\"></div></template>\n";
        const string malformedCatalog =
            "{\"version\":\"1\",\"entries\":[],\"truncated\":false}";
        var service = CreateService(
            source,
            malformedCatalog,
            CreateCatalog(
                false,
                new CatalogEntry(
                    "catalog-valid",
                    ".catalog-valid { display: block; }")));

        var completionList = service.GetCompletionList(
            DocumentUri,
            PositionAfter(source, "class=\"catalog-"));

        completionList.Items.Single().Label.ShouldBe("catalog-valid");
        completionList.IsIncomplete.ShouldBeFalse();
    }

    private static ILanguageService CreateService(
        string source,
        params string[] catalogs)
    {
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IClassCatalogLanguageService>()
            .ConfigureClassCatalogs(
                DocumentUri,
                new LanguageClassCatalogConfiguration(catalogs));
        service.OpenDocument(DocumentUri, source, 1);
        return service;
    }

    private static string CreateCatalog(
        bool truncated,
        params CatalogEntry[] entries)
    {
        var jsonEntries = new JsonArray();
        foreach (var entry in entries)
        {
            var jsonEntry = new JsonObject
            {
                ["class"] = entry.ClassName,
                ["css"] = entry.Css,
            };
            if (entry.ColorValue is not null)
            {
                jsonEntry["colorValue"] = entry.ColorValue;
            }

            if (entry.SortText is not null)
            {
                jsonEntry["sortText"] = entry.SortText;
            }

            jsonEntries.Add(jsonEntry);
        }

        return new JsonObject
        {
            ["version"] = 1,
            ["entries"] = jsonEntries,
            ["truncated"] = truncated,
        }.ToJsonString();
    }

    private static LanguagePosition PositionAfter(string source, string marker)
    {
        var markerOffset = source.IndexOf(marker, StringComparison.Ordinal);
        markerOffset.ShouldBeGreaterThanOrEqualTo(0);
        return TextCoordinateConverter.GetPosition(source, markerOffset + marker.Length);
    }

    private sealed record CatalogEntry(
        string ClassName,
        string Css,
        string? ColorValue = null,
        string? SortText = null);
}
