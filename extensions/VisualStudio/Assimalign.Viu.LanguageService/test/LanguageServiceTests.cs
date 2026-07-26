using System;
using System.Linq;

using Shouldly;

using Xunit;

using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.LanguageService;

public class LanguageServiceTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";
    private const string VueDocumentUri = "file:///workspace/Counter.vue";

    [Fact]
    public void GetDiagnostics_MalformedBlockHeader_ProjectsParserDiagnostic()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "@script\n", 1);

        var diagnostics = service.GetDiagnostics(DocumentUri);

        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Code.ShouldBe("VIU1003");
        diagnostics[0].Severity.ShouldBe(LanguageDiagnosticSeverity.Error);
        diagnostics[0].Range.Start.ShouldBe(new LanguagePosition(0, 0));
    }

    [Fact]
    public void ChangeDocument_RangedEditRepairsSourceAndRefreshesParseResult()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "@script\n", 1);

        var changed = service.ChangeDocument(
            DocumentUri,
            2,
            [
                new LanguageDocumentChange(
                    new LanguageRange(
                        new LanguagePosition(0, 7),
                        new LanguagePosition(0, 7)),
                    " {\n}"),
            ]);

        changed.ShouldBeTrue();
        service.GetDiagnostics(DocumentUri).ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_RootWithTemplate_DoesNotOfferDuplicateSingletonBlock()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(
            DocumentUri,
            "@template {\n    <div/>\n}\n",
            1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(3, 0));

        completions.ShouldNotContain(item => item.Label == "@template");
        completions.ShouldContain(item => item.Label == "@script");
        completions.ShouldContain(item => item.Label == "@style");
    }

    [Fact]
    public void GetCompletions_TemplateDirectiveContext_OffersVueCompatibleDirectives()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(
            DocumentUri,
            "@template {\n    <div v-\n}\n",
            1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, 11));

        completions.ShouldContain(item => item.Label == "v-if");
        completions.ShouldContain(item => item.Label == "v-for");
        completions.ShouldNotContain(item => item.Label == "@script");
    }

    [Fact]
    public void GetCompletions_StaticClassCandidate_UsesSharedUtilityRegistryAndExactEditRange()
    {
        const string templateLine = "    <div class=\"flex gap-\"></div>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var candidateStart = templateLine.IndexOf("gap-", StringComparison.Ordinal);
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, candidateStart + "gap-".Length));

        var gap = completions.Single(item => item.Label == "gap-4");
        gap.Detail.ShouldBe("Viu utility class");
        gap.Documentation.ShouldContain(
            "gap: calc(var(--spacing) * 4);");
        gap.EditRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, candidateStart),
                new LanguagePosition(1, candidateStart + "gap-".Length)));
        gap.FilterText.ShouldBe("gap-4");
    }

    [Fact]
    public void GetCompletions_ProjectTheme_OffersCustomTokenFromCompilerRegistry()
    {
        const string templateLine =
            "    <section class=\"p-car\"></section>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var candidateStart = templateLine.IndexOf(
            "p-car",
            StringComparison.Ordinal);
        var service = ViuLanguageServices.Create();
        var utilityCssLanguageService =
            service.ShouldBeAssignableTo<IUtilityCssLanguageService>();
        utilityCssLanguageService.ConfigureUtilityStylesheet(
            DocumentUri,
            "@theme { --spacing-card: 2.75rem; }");
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(
                1,
                candidateStart + "p-car".Length));

        var customSpacing = completions.Single(
            item => item.Label == "p-card");
        customSpacing.Documentation.ShouldContain(
            "padding: var(--spacing-card);");
    }

    [Fact]
    public void GetHover_ImportPrefixAndInlineTheme_UsesSharedCssFirstConfiguration()
    {
        const string candidate = "vu:bg-blue-500";
        var source =
            $"@template {{\n    <div class=\"{candidate}\"></div>\n}}\n";
        var service = ViuLanguageServices.Create();
        service.ShouldBeAssignableTo<IUtilityCssLanguageService>()
            .ConfigureUtilityStylesheet(
                DocumentUri,
                "@import \"viu-utilities\" prefix(vu) theme(inline) important;");
        service.OpenDocument(DocumentUri, source, 1);

        var hover = service.GetHover(
            DocumentUri,
            new LanguagePosition(
                1,
                "    <div class=\"".Length + 4));

        hover.ShouldNotBeNull();
        hover.Markdown.ShouldContain("background-color:");
        hover.Markdown.ShouldNotContain("var(--vu-color-blue-500)");
        hover.Markdown.ShouldContain("!important");
    }

    [Fact]
    public void GetCompletions_ProjectUtility_UsesExecutableCustomDefinition()
    {
        const string candidatePrefix = "brand-";
        var source =
            $"@template {{\n    <div class=\"{candidatePrefix}\"></div>\n}}\n";
        var service = ViuLanguageServices.Create();
        service.ShouldBeAssignableTo<IUtilityCssLanguageService>()
            .ConfigureUtilityStylesheet(
                DocumentUri,
                """
                @utility brand-surface {
                  background-color: rebeccapurple;
                }
                """);
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(
                1,
                "    <div class=\"".Length + candidatePrefix.Length));

        var custom = completions.Single(
            item => item.Label == "brand-surface");
        custom.Documentation.ShouldContain(
            "background-color: rebeccapurple;");
    }

    [Fact]
    public void GetCompletions_ProjectVariant_CombinesWithBuiltInUtility()
    {
        const string candidatePrefix = "theme-midnight:bg-blue-";
        var source =
            $"@template {{\n    <div class=\"{candidatePrefix}\"></div>\n}}\n";
        var service = ViuLanguageServices.Create();
        service.ShouldBeAssignableTo<IUtilityCssLanguageService>()
            .ConfigureUtilityStylesheet(
                DocumentUri,
                """
                @custom-variant theme-midnight (&:where([data-theme="midnight"] *));
                """);
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(
                1,
                "    <div class=\"".Length + candidatePrefix.Length));

        var customVariant = completions.Single(
            item => item.Label == "theme-midnight:bg-blue-500");
        customVariant.Documentation.ShouldContain(
            "&:where([data-theme=\"midnight\"] *)");
        customVariant.Documentation.ShouldContain(
            "background-color: var(--color-blue-500);");
    }

    [Fact]
    public void GetCompletions_ReferencedProjectUtility_UsesResolvedEditorGraph()
    {
        const string candidatePrefix = "shared-";
        var source =
            $"@template {{\n    <div class=\"{candidatePrefix}\"></div>\n}}\n";
        var graph = new UtilityStylesheetReferenceGraph(
            new[]
            {
                new UtilityStylesheetReference(
                    "application.css",
                    "./shared.css",
                    "shared.css",
                    """
                    @utility shared-card {
                      border-color: rebeccapurple;
                    }
                    """),
            });
        var service = ViuLanguageServices.Create();
        service.ShouldBeAssignableTo<IUtilityCssLanguageService>()
            .ConfigureUtilityStylesheet(
                DocumentUri,
                "@reference \"./shared.css\";",
                "application.css",
                graph);
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(
                1,
                "    <div class=\"".Length + candidatePrefix.Length));

        completions.Single(
                item => item.Label == "shared-card")
            .Documentation.ShouldContain(
                "border-color: rebeccapurple;");
    }

    [Fact]
    public void GetCompletions_BoundClassLiteral_OffersUtilityWithoutEvaluatingExpression()
    {
        const string templateLine =
            "    <div :class=\"['bg-blue-', active ? 'flex' : 'hidden']\"></div>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var candidateStart = templateLine.IndexOf("bg-blue-", StringComparison.Ordinal);
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, candidateStart + "bg-blue-".Length));

        completions.ShouldContain(item => item.Label == "bg-blue-500");
        completions.ShouldNotContain(item => item.Label == "@script");
    }

    [Fact]
    public void GetHover_UtilityClass_ReturnsCompilerCssFromSharedRegistry()
    {
        const string candidate = "hover:bg-blue-500";
        var templateLine = $"    <button class=\"{candidate}\"></button>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var candidateStart = templateLine.IndexOf(candidate, StringComparison.Ordinal);
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var hover = service.GetHover(
            DocumentUri,
            new LanguagePosition(1, candidateStart + 8));

        hover.ShouldNotBeNull();
        hover!.Markdown.ShouldContain("@media (hover: hover)");
        hover.Markdown.ShouldContain("background-color:");
        hover.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, candidateStart),
                new LanguagePosition(1, candidateStart + candidate.Length)));
    }

    [Fact]
    public void GetCompletions_TemplateTextOutsideClass_DoesNotOfferUtilityClasses()
    {
        const string templateLine = "    <p>gap-</p>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var candidateEnd = templateLine.IndexOf("gap-", StringComparison.Ordinal) + "gap-".Length;
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, candidateEnd));

        completions.ShouldNotContain(item => item.Label == "gap-4");
    }

    [Fact]
    public void GetCompletions_VueTemplateClass_UsesTagBasedDescriptorAndUtilityRegistry()
    {
        const string templateLine = "  <div class=\"flex gap-\"></div>";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var candidateStart = templateLine.IndexOf("gap-", StringComparison.Ordinal);
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var completions = service.GetCompletions(
            VueDocumentUri,
            new LanguagePosition(1, candidateStart + "gap-".Length));

        var gap = completions.Single(item => item.Label == "gap-4");
        gap.EditRange.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, candidateStart),
                new LanguagePosition(1, candidateStart + "gap-".Length)));
    }

    [Fact]
    public void GetCompletions_VueRoot_OffersTagBasedBlocksAndSkipsExistingTemplate()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(
            VueDocumentUri,
            "<template><div /></template>\n",
            1);

        var completions = service.GetCompletions(
            VueDocumentUri,
            new LanguagePosition(1, 0));

        completions.ShouldNotContain(item => item.Label == "template");
        completions.ShouldContain(item => item.Label == "script");
        completions.ShouldContain(item => item.Label == "script setup");
        completions.ShouldContain(item => item.Label == "style");
        completions.ShouldNotContain(item => item.Label == "@script");
    }

    [Fact]
    public void GetCompletions_VueRoot_OffersOnlyMissingOrdinaryAndSetupScriptSlots()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(
            VueDocumentUri,
            "<script lang=\"csharp\">public int Ordinary;</script>\n",
            1);

        var afterOrdinary = service.GetCompletions(
            VueDocumentUri,
            new LanguagePosition(1, 0));
        afterOrdinary.ShouldNotContain(item => item.Label == "script");
        afterOrdinary.ShouldContain(item => item.Label == "script setup");

        service.OpenDocument(
            VueDocumentUri,
            "<script setup lang=\"csharp\">public int Setup;</script>\n",
            2);
        var afterSetup = service.GetCompletions(
            VueDocumentUri,
            new LanguagePosition(1, 0));
        afterSetup.ShouldContain(item => item.Label == "script");
        afterSetup.ShouldNotContain(item => item.Label == "script setup");
    }

    [Fact]
    public void GetDiagnostics_VueJavaScriptAndCSharpScriptSetup_ReportsOnlyUnsupportedLanguage()
    {
        const string source =
            "<script>export default {}</script>\n" +
            "<script setup lang=\"csharp\">public int Count;</script>\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var diagnostics = service.GetDiagnostics(VueDocumentUri);

        diagnostics.ShouldHaveSingleItem().Code.ShouldBe("VIU1206");
    }

    [Fact]
    public void GetCompletions_VueCSharpScript_UsesExistingScriptCatalog()
    {
        const string source =
            "<script lang=\"csharp\">\n" +
            "  Context.\n" +
            "</script>\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var completions = service.GetCompletions(
            VueDocumentUri,
            new LanguagePosition(1, 10));

        service.GetDiagnostics(VueDocumentUri).ShouldBeEmpty();
        completions.ShouldContain(item => item.Label == "Lifecycle");
        completions.ShouldContain(item => item.Label == "Emit");
    }

    [Fact]
    public void GetCompletions_ComponentContextMemberAccess_OffersContextContract()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(
            DocumentUri,
            "@script {\n    Context.\n}\n",
            1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, 12));

        completions.ShouldContain(item => item.Label == "Lifecycle");
        completions.ShouldContain(item => item.Label == "Services");
        completions.ShouldContain(item => item.Label == "Emit");
        completions.All(item => item.Label != "@template").ShouldBeTrue();
    }

    [Fact]
    public void GetCompletions_StyleHeader_OffersStyleBlockOptions()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "@style ", 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(0, 7));

        completions.ShouldContain(item => item.Label == "scoped");
        completions.ShouldContain(item => item.Label == "module");
        completions.ShouldContain(item => item.Label == "lang=\"css\"");
    }

    [Fact]
    public void GetHover_ReactiveReference_ReturnsDeveloperDocumentationAndTokenRange()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(
            DocumentUri,
            "@script {\n    Reactive.Reference(0);\n}\n",
            1);

        var hover = service.GetHover(
            DocumentUri,
            new LanguagePosition(1, 18));

        hover.ShouldNotBeNull();
        hover!.Markdown.ShouldContain("Reference<T>");
        hover.Range.Start.ShouldBe(new LanguagePosition(1, 4));
        hover.Range.End.ShouldBe(new LanguagePosition(1, 22));
    }

    [Fact]
    public void CloseDocument_OpenDocument_RemovesLanguageResults()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "@script\n", 1);

        service.CloseDocument(DocumentUri).ShouldBeTrue();

        service.GetDiagnostics(DocumentUri).ShouldBeEmpty();
        service.GetCompletions(DocumentUri, new LanguagePosition(0, 0)).ShouldBeEmpty();
    }
}
