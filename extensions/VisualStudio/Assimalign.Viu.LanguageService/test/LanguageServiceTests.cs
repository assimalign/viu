using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService;

public class LanguageServiceTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";

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
