using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the static catalogs against the root-level lifecycle registration surface
/// (<c>[CMP-32]</c>): every component inherits <c>OnMounted(...)</c> and its siblings from
/// <c>ComponentTemplateBase</c>, so the shorter root form is the idiom the scaffold snippet
/// inserts, the compilation-free answer offers, and hover documents. The
/// <c>Context.Lifecycle</c> form registers exactly the same callback and stays valid.
/// </summary>
public class ScriptLifecycleCatalogTests
{
    private const string DocumentUri = "file:///workspace/AppShell.viu";

    private const string ComponentSource = "@script {\n    \n}\n";

    [Fact]
    public void GetCompletions_MountedCallbackSnippet_InsertsTheRootForm()
    {
        var completions = Complete();

        var snippet = completions.Single(completion => completion.Label == "mounted callback");
        snippet.InsertText.ShouldBe("OnMounted($1);");
        snippet.IsSnippet.ShouldBeTrue();
        // The context form is not withdrawn — it registers the same callback — so the item says so.
        snippet.Documentation.ShouldContain("Context.Lifecycle.OnMounted");
    }

    [Fact]
    public void GetCompletions_WithoutProjectContext_OffersRootLifecycleRegistrations()
    {
        // Degraded mode still knows the idiomatic surface: with no compilation the semantic engine
        // cannot bind the inherited members, and the static catalog is what is left.
        var completions = Complete();

        foreach (var label in new[]
                 {
                     "OnBeforeMount",
                     "OnMounted",
                     "OnBeforeUpdate",
                     "OnUpdated",
                     "OnBeforeUnmount",
                     "OnUnmounted",
                     "OnErrorCaptured",
                     "OnServerPrefetch",
                     "OnActivated",
                     "OnDeactivated",
                 })
        {
            var item = completions.Single(completion => completion.Label == label);
            item.Kind.ShouldBe(LanguageCompletionItemKind.Method);
            item.IsSnippet.ShouldBeFalse();
            item.InsertText.ShouldBe(label);
            // Below the scaffold band ("01"–"07"), above the keyword band ("90").
            item.SortText.ShouldBe("08");
            item.Detail.ShouldStartWith($"void {label}(");
            item.Documentation.ShouldContain("[CMP-32]");
            item.Documentation.ShouldContain($"Context.Lifecycle.{label}");
        }
    }

    [Fact]
    public void GetCompletions_PartiallyTypedRootLifecycleForm_IsFilteredLikeAnyOtherItem()
    {
        var service = OpenService("@script {\n    OnMou\n}\n");

        var completions = service.GetCompletions(DocumentUri, new LanguagePosition(1, 9));

        completions.ShouldContain(completion => completion.Label == "OnMounted");
        completions.ShouldAllBe(
            completion => completion.Label.StartsWith("OnMou", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetHover_RootLifecycleForm_DocumentsTheRegistrationAndItsContextEquivalent()
    {
        var service = OpenService("@script {\n    OnMounted(Load);\n}\n");

        var hover = service.GetHover(DocumentUri, new LanguagePosition(1, 8));

        hover.ShouldNotBeNull();
        hover.Markdown.ShouldContain("OnMounted");
        hover.Markdown.ShouldContain("Context.Lifecycle.OnMounted");
        hover.Markdown.ShouldContain("[CMP-32]");
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageCompletionItem> Complete()
        => OpenService(ComponentSource).GetCompletions(DocumentUri, new LanguagePosition(1, 4));

    private static ILanguageService OpenService(string source)
    {
        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);
        return service;
    }
}
