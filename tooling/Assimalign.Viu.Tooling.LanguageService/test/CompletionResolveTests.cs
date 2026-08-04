using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins the deferred completion-documentation contract: utility completion items ship without
/// documentation bodies, and <see cref="IViuLanguageService.ResolveCompletionDocumentation"/>
/// recomputes the body on demand through the same path hover uses.
/// </summary>
public class CompletionResolveTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";

    [Fact]
    public void GetCompletions_UtilityCandidate_DefersDocumentationToResolve()
    {
        const string templateLine = "    <div class=\"gap-\"></div>";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var candidateEnd =
            templateLine.IndexOf("gap-", StringComparison.Ordinal) + "gap-".Length;
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, candidateEnd));

        var gap = completions.Single(item => item.Label == "gap-4");
        gap.Documentation.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveCompletionDocumentation_BuiltInUtilityCandidate_ReturnsCssBlockDocumentation()
    {
        var service = ViuLanguageServices.Create();

        var documentation = service.ResolveCompletionDocumentation(DocumentUri, "gap-4");

        documentation.ShouldNotBeNull();
        documentation.ShouldContain("gap: calc(var(--spacing) * 4);");
    }

    [Fact]
    public void ResolveCompletionDocumentation_ProjectUtilityCandidate_UsesConfiguredStylesheet()
    {
        var service = ViuLanguageServices.Create();
        service.ShouldBeAssignableTo<IUtilityCssLanguageService>()
            .ConfigureUtilityStylesheet(
                DocumentUri,
                """
                @utility brand-surface {
                  background-color: rebeccapurple;
                }
                """);

        var documentation = service.ResolveCompletionDocumentation(
            DocumentUri,
            "brand-surface");

        documentation.ShouldNotBeNull();
        documentation.ShouldContain("background-color: rebeccapurple;");
    }

    [Fact]
    public void ResolveCompletionDocumentation_UnknownLabel_ReturnsNull()
    {
        var service = ViuLanguageServices.Create();

        // Catalog labels resolve nothing (their one-line documentation ships inline), and garbage
        // resolves nothing.
        service.ResolveCompletionDocumentation(DocumentUri, "@template").ShouldBeNull();
        service.ResolveCompletionDocumentation(DocumentUri, "not-a-real-utility-candidate")
            .ShouldBeNull();
    }
}
