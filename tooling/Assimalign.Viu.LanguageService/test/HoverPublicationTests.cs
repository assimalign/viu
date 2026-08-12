using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the editor-neutral hover surfaces published for every authored single-file-component block
/// ([V01.01.12.07.16], #337). Host adapters transport these ranges and Markdown bodies unchanged.
/// </summary>
public sealed class HoverPublicationTests
{
    [Fact]
    public void GetHover_ProjectedScriptSymbol_ReturnsRoslynSignatureAtAuthoredReference()
    {
        const string source =
            "@script {\n" +
            "    /// <summary>Number of authored items.</summary>\n" +
            "    public int Count { get; } = 1;\n" +
            "    public int Read() => Count;\n" +
            "}\n";
        const string reference = "Count";
        var referenceStart = "    public int Read() => ".Length;
        var service = CreateSemanticService(source);

        var hover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(3, referenceStart + 2));

        hover.ShouldNotBeNull();
        hover!.Markdown.ShouldContain("int Count");
        hover.Markdown.ShouldContain("Number of authored items.");
        hover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(3, referenceStart),
                new LanguagePosition(3, referenceStart + reference.Length)));
    }

    [Fact]
    public void GetHover_ComponentTagParameterAndEvent_ReturnResolvedContract()
    {
        const string componentSource =
            "<template><article></article></template>\n" +
            "@script {\n" +
            "    using Assimalign.Viu.Components;\n" +
            "    [Parameter(IsRequired = true)]\n" +
            "    public string Title { get; set; } = string.Empty;\n" +
            "    [Event]\n" +
            "    public partial void Selected(int identifier);\n" +
            "}\n";
        const string templateLine =
            "  <FeatureCard title=\"Welcome\" @selected=\"Handle\" />";
        var source =
            $"<template>\n{templateLine}\n</template>\n" +
            "@script {\n" +
            "    public void Handle(int identifier) { }\n" +
            "}\n";
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(
                ScriptSemanticFixture.DocumentUri,
                ScriptSemanticFixture.CreateContext(
                    new LanguageProjectSourceDocument(
                        "C:/workspace/App/FeatureCard.viu",
                        componentSource,
                        IsComponent: true)));
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var tagStart = templateLine.IndexOf("FeatureCard", StringComparison.Ordinal);
        var parameterStart = templateLine.IndexOf("title", StringComparison.Ordinal);
        var eventStart = templateLine.IndexOf("selected", StringComparison.Ordinal);
        var tagHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, tagStart + 2));
        var parameterHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, parameterStart + 2));
        var eventHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, eventStart + 2));

        // The Quick Info shape ([V01.01.12.07.16]): the declaration leads, fenced so a client
        // colorizes it and renders it at body size, and the prose follows. Bold text and bullet lists
        // are a document's structure and a client renders them at heading and list sizes, which is
        // what made a Viu tooltip read as something other than the editor's own.
        tagHover.ShouldNotBeNull();
        tagHover!.Markdown.ShouldStartWith("```csharp\nclass FeatureCard\n");
        tagHover.Markdown.ShouldContain("    string Title   // :title (required)");
        tagHover.Markdown.ShouldContain(
            "    public partial void Selected(int identifier)   // @selected");
        tagHover.Markdown.ShouldContain(
            "```\nResolves the `<FeatureCard>` tag to component `FeatureCard`.");
        tagHover.Markdown.ShouldNotContain("**");
        tagHover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, tagStart),
                new LanguagePosition(1, tagStart + "FeatureCard".Length)));

        parameterHover.ShouldNotBeNull();
        parameterHover!.Markdown.ShouldStartWith("```csharp\nstring Title\n```\n");
        parameterHover.Markdown.ShouldContain("parameter on component `<FeatureCard>`");
        parameterHover.Markdown.ShouldContain("Required.");
        parameterHover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, parameterStart),
                new LanguagePosition(1, parameterStart + "title".Length)));

        eventHover.ShouldNotBeNull();
        eventHover!.Markdown.ShouldStartWith(
            "```csharp\npublic partial void Selected(int identifier)\n```\n");
        eventHover.Markdown.ShouldContain("event on component `<FeatureCard>`");
        eventHover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, eventStart),
                new LanguagePosition(1, eventStart + "selected".Length)));
    }

    [Fact]
    public void GetHover_NativeElementAndDirective_ReturnTemplateDocumentation()
    {
        const string templateLine = "  <section v-show=\"Visible\"></section>";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var elementStart = templateLine.IndexOf("section", StringComparison.Ordinal);
        var directiveStart = templateLine.IndexOf("v-show", StringComparison.Ordinal);

        var elementHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, elementStart + 2));
        var directiveHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, directiveStart + 2));

        elementHover.ShouldNotBeNull();
        elementHover!.Markdown.ShouldContain("native HTML element");
        elementHover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, elementStart),
                new LanguagePosition(1, elementStart + "section".Length)));
        directiveHover.ShouldNotBeNull();
        directiveHover!.Markdown.ShouldContain("Toggles element visibility");
        directiveHover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, directiveStart),
                new LanguagePosition(1, directiveStart + "v-show".Length)));
    }

    [Fact]
    public void GetHover_TemplateHtmlComment_ReturnsNoHoverForElementOrDirective()
    {
        const string templateLine = "  <!-- <button v-if=\"Legacy\"> -->";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var elementStart = templateLine.IndexOf("button", StringComparison.Ordinal);
        var directiveStart = templateLine.IndexOf("v-if", StringComparison.Ordinal);

        var elementHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, elementStart + 2));
        var directiveHover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, directiveStart + 2));

        elementHover.ShouldBeNull();
        directiveHover.ShouldBeNull();
    }

    [Fact]
    public void GetHover_UnresolvedUppercaseNativeCollision_DoesNotDescribeNativeElement()
    {
        const string templateLine = "  <Button></Button>";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var tagStart = templateLine.IndexOf("Button", StringComparison.Ordinal);

        var hover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, tagStart + 2));

        hover.ShouldBeNull();
    }

    [Fact]
    public void GetHover_StyleBlockClassCollidingWithUtility_ReturnsAuthoredDeclarationAtTemplateToken()
    {
        const string templateLine = "  <div class=\"flex\"></div>";
        const string declaration = ".flex { display: block; }";
        var source =
            $"<template>\n{templateLine}\n</template>\n" +
            $"<style>\n{declaration}\n</style>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var classStart = templateLine.IndexOf("flex", StringComparison.Ordinal);

        var hover = service.GetHover(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(1, classStart + 4));

        hover.ShouldNotBeNull();
        hover!.Markdown.ShouldContain("declared in this component's style block");
        hover.Markdown.ShouldContain(declaration);
        hover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, classStart),
                new LanguagePosition(1, classStart + "flex".Length)));
    }

    private static ILanguageService CreateSemanticService(string source)
    {
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(
                ScriptSemanticFixture.DocumentUri,
                ScriptSemanticFixture.CreateContext());
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        return service;
    }
}
