using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the deferred completion-documentation contract for semantic script symbols.
/// </summary>
public class CompletionResolveTests
{
    private const string DocumentUri = ScriptSemanticFixture.DocumentUri;

    private const string Source =
        "@script {\n" +
        "/// <summary>Counts clicks.</summary>\n" +
        "public int Count { get; set; }\n" +
        "    \n" +
        "}\n";

    [Fact]
    public void GetCompletions_SemanticSymbol_DefersDocumentationToResolve()
    {
        var service = CreateService();

        var count = service.GetCompletions(DocumentUri, CompletionPosition())
            .Single(item => item.Label == "Count");

        count.Documentation.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveCompletionDocumentation_SemanticSymbol_ReturnsSymbolDocumentation()
    {
        var service = CreateService();
        service.GetCompletions(DocumentUri, CompletionPosition());

        var documentation = service.ResolveCompletionDocumentation(DocumentUri, "Count");

        documentation.ShouldNotBeNull();
        documentation.ShouldContain("int Count { get; set; }");
        documentation.ShouldContain("Counts clicks.");
    }

    [Fact]
    public void ResolveCompletionDocumentation_UnknownLabel_ReturnsNull()
    {
        var service = CreateService();
        service.GetCompletions(DocumentUri, CompletionPosition());

        service.ResolveCompletionDocumentation(DocumentUri, "UnknownSymbol").ShouldBeNull();
    }

    private static ILanguageService CreateService()
    {
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(DocumentUri, ScriptSemanticFixture.CreateContext());
        service.OpenDocument(DocumentUri, Source, 1);
        return service;
    }

    private static LanguagePosition CompletionPosition()
    {
        var marker = "set; }\n    ";
        var offset = Source.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return TextCoordinateConverter.GetPosition(Source, offset);
    }
}
