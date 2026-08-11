using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins authored-position publication for template, projected C#, style, and component-usage
/// diagnostics ([V01.01.12.07.15], #336).
/// </summary>
public sealed class DiagnosticPublicationTests
{
    [Fact]
    public void GetDiagnostics_TemplateCompilerError_MapsIntoTemplateBlock()
    {
        const string source =
            "<template>\n" +
            "  <div\n" +
            "</template>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var diagnostic = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri)
            .First(item => item.Code.StartsWith("VIU11", StringComparison.Ordinal));

        diagnostic.Source.ShouldBe("viu");
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostic.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(2, 0),
                new LanguagePosition(2, 0)));
    }

    [Fact]
    public void GetDiagnostics_ProjectedScriptBindingError_MapsRoslynSpanIntoScriptBlock()
    {
        const string scriptLine = "    public int Count => MissingValue;";
        var source = $"@script {{\n{scriptLine}\n}}\n";
        var missingStart = scriptLine.IndexOf("MissingValue", StringComparison.Ordinal);
        var service = CreateSemanticService(source);

        var diagnostic = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri)
            .Single(item => item.Code == "CS0103");

        diagnostic.Source.ShouldBe("csharp");
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostic.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, missingStart),
                new LanguagePosition(1, missingStart + "MissingValue".Length)));
    }

    [Theory]
    [InlineData(
        "VIU1204",
        "Context",
        "<template>\n" +
        "    <div />\n" +
        "</template>\n" +
        "@script {\n" +
        "    private object Context = new();\n" +
        "}\n")]
    [InlineData(
        "VIU1205",
        "SaveAsync",
        "@script {\n" +
        "    private async void SaveAsync()\n" +
        "    {\n" +
        "        await global::System.Threading.Tasks.Task.Yield();\n" +
        "    }\n" +
        "}\n")]
    [InlineData(
        "VIU1207",
        "Title",
        "<template><div /></template>\n" +
        "@script {\n" +
        "    public object Parameters { get; } = new();\n" +
        "    [Parameter] public string Title { get; set; } = \"\";\n" +
        "}\n")]
    [InlineData(
        "VIU1208",
        "Heading",
        "<template><div /></template>\n" +
        "@script {\n" +
        "    [Parameter] public string Title { get; set; } = \"\";\n" +
        "    [Parameter(\"title\")] public string Heading { get; set; } = \"\";\n" +
        "}\n")]
    [InlineData(
        "VIU1209",
        "Title",
        "<template><div /></template>\n" +
        "@script {\n" +
        "    [Parameter] public string Title => \"\";\n" +
        "}\n")]
    public void GetDiagnostics_ScriptProjectionRule_PublishesOnlyBuildRuleAtAuthoredMember(
        string expectedCode,
        string authoredMember,
        string source)
    {
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var scriptBandDiagnostics = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri)
            .Where(item => item.Code.StartsWith("VIU12", StringComparison.Ordinal))
            .ToArray();

        var diagnostic = scriptBandDiagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(expectedCode);
        diagnostic.Source.ShouldBe("viu");
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostic.Range.ShouldBe(GetAuthoredRange(source, authoredMember));
    }

    [Fact]
    public void GetDiagnostics_StyleParserError_MapsIntoStyleBlock()
    {
        const string styleLine = ".card { color red; margin: 0; }";
        var source = $"<style>\n{styleLine}\n</style>\n";
        var errorStart = styleLine.IndexOf("color red", StringComparison.Ordinal);
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var diagnostic = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri)
            .First(item => item.Code.StartsWith("VIU13", StringComparison.Ordinal));

        diagnostic.Source.ShouldBe("viu");
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostic.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, errorStart),
                new LanguagePosition(1, errorStart + "color red".Length)));
    }

    [Fact]
    public void GetDiagnostics_UnresolvedComponent_MapsViu1404ToAuthoredTag()
    {
        const string templateLine = "  <MissingCard />";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var tagStart = templateLine.IndexOf("MissingCard", StringComparison.Ordinal);
        var service = CreateSemanticService(source);

        var diagnostic = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri)
            .Single(item => item.Code == "VIU1404");

        diagnostic.Source.ShouldBe("viu");
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostic.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, tagStart),
                new LanguagePosition(1, tagStart + "MissingCard".Length)));
    }

    [Fact]
    public void GetDiagnostics_LooseFileComponent_DoesNotTreatEmptyCatalogAsAuthoritative()
    {
        const string source =
            "<template>\n" +
            "  <ProjectComponent />\n" +
            "</template>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var diagnostics = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri);

        diagnostics.ShouldNotContain(item => item.Code.StartsWith("VIU14", StringComparison.Ordinal));
    }

    [Fact]
    public void GetDiagnostics_UnresolvedProjectCatalog_DoesNotTreatSemanticMissAsEmptyCatalog()
    {
        const string source =
            "<template>\n" +
            "  <ProjectComponent />\n" +
            "</template>\n";
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(
                ScriptSemanticFixture.DocumentUri,
                new LanguageProjectContext(
                    ScriptSemanticFixture.ProjectFilePath,
                    ScriptSemanticFixture.ProjectDirectory,
                    ScriptSemanticFixture.RootNamespace,
                    ["C:/workspace/App/missing-reference.dll"],
                    Array.Empty<LanguageProjectSourceDocument>(),
                    Array.Empty<string>(),
                    "unresolved"));
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var diagnostics = service.GetDiagnostics(ScriptSemanticFixture.DocumentUri);

        diagnostics.ShouldNotContain(item => item.Code.StartsWith("VIU14", StringComparison.Ordinal));
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

    private static LanguageRange GetAuthoredRange(string source, string authoredMember)
    {
        var offset = source.IndexOf(authoredMember, StringComparison.Ordinal);
        offset.ShouldBeGreaterThanOrEqualTo(0);
        var prefix = source.Substring(0, offset);
        var line = prefix.Count(character => character == '\n');
        var previousLineBreak = prefix.LastIndexOf('\n');
        var character = offset - previousLineBreak - 1;
        return new LanguageRange(
            new LanguagePosition(line, character),
            new LanguagePosition(line, character + authoredMember.Length));
    }
}
