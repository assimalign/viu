using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins declared-member completion inside an <c>@script</c> block ([V01.01.12.07.04], #261): a
/// syntax-only Roslyn parse of the script block offers the file's own fields, properties, and
/// methods above the scaffold and keyword catalogs. The parse is syntax-only on purpose: completion
/// must stay responsive while the file is mid-edit and therefore uncompilable.
/// </summary>
public class ScriptDeclarationCompletionTests
{
    private const string DocumentUri = "file:///workspace/AppShell.viu";
    private const string VueDocumentUri = "file:///workspace/AppShell.vue";

    [Fact]
    public void GetCompletions_DeclaredField_OffersFieldWithDeclaredTypeDetail()
    {
        var completions = CompleteInScript(
            "public Reference<int> Counter = Reactive.Reference(0);");

        var item = completions.Single(completion => completion.Label == "Counter");
        item.Kind.ShouldBe(LanguageCompletionItemKind.Field);
        item.Detail.ShouldBe("Reference<int>");
    }

    [Fact]
    public void GetCompletions_DeclaredProperty_OffersPropertyKindWithTypeDetail()
    {
        var completions = CompleteInScript(
            "public string Title { get; } = \"Viu\";");

        var item = completions.Single(completion => completion.Label == "Title");
        item.Kind.ShouldBe(LanguageCompletionItemKind.Property);
        item.Detail.ShouldBe("string");
    }

    [Fact]
    public void GetCompletions_DeclaredMethod_OffersMethodWithSignatureDetail()
    {
        var completions = CompleteInScript(
            "public async Task SaveAsync(int id) { }");

        var item = completions.Single(completion => completion.Label == "SaveAsync");
        item.Kind.ShouldBe(LanguageCompletionItemKind.Method);
        item.Detail.ShouldBe("Task SaveAsync(int id)");
    }

    [Fact]
    public void GetCompletions_MultipleFieldDeclarators_OffersEveryVariable()
    {
        var completions = CompleteInScript("private int width, height;");

        completions.ShouldContain(item => item.Label == "width");
        completions.ShouldContain(item => item.Label == "height");
    }

    [Fact]
    public void GetCompletions_TypedPrefix_FiltersDeclaredMembersWithKeywords()
    {
        var completions = CompleteInScript(
            "public Reference<int> Counter = Reactive.Reference(0);",
            typedLine: "    co");

        completions.ShouldContain(item => item.Label == "Counter");
        completions.ShouldContain(item => item.Label == "const");
        completions.ShouldContain(item => item.Label == "continue");
        completions.ShouldAllBe(
            item => item.Label.StartsWith("co", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetCompletions_DeclaredMember_SortsAboveKeywordsAndScaffold()
    {
        var completions = CompleteInScript(
            "public Reference<int> Counter = Reactive.Reference(0);");

        var declared = completions.Single(item => item.Label == "Counter");
        foreach (var other in completions.Where(item => item.Label != "Counter"))
        {
            string.CompareOrdinal(declared.SortText, other.SortText).ShouldBeLessThan(0);
        }
    }

    [Fact]
    public void GetCompletions_LeadingUsingDirectives_StillExtractsMembers()
    {
        // Pins the region split mirrored from ScriptBlockAnalyzer: hoisted usings are illegal
        // inside the probe class and must be cut before the member parse.
        var completions = CompleteInScript(
            "using System;\nusing System.Threading.Tasks;\n\npublic int Counter;");

        completions.ShouldContain(item => item.Label == "Counter");
    }

    [Fact]
    public void GetCompletions_MalformedMember_StillOffersWellFormedSiblings()
    {
        // Pins Roslyn error recovery: a broken member must not take its well-formed siblings (or
        // the request) down with it.
        var completions = CompleteInScript(
            "public int Broken(\npublic string Title { get; } = \"Viu\";");

        completions.ShouldContain(item => item.Label == "Title");
    }

    [Fact]
    public void GetCompletions_DocumentedMember_CarriesFirstSummaryLine()
    {
        var completions = CompleteInScript(
            "/// <summary>Counts clicks.</summary>\npublic int Counter;");

        completions.Single(item => item.Label == "Counter")
            .Documentation.ShouldBe("Counts clicks.");
    }

    [Fact]
    public void GetCompletions_ScriptAndScriptSetupBlocks_OfferUnionOfBothBlocks()
    {
        // A .vue file's plain and setup scripts merge into one generated partial class, so a
        // completion in either block legitimately sees both blocks' declarations.
        const string source =
            "<script lang=\"csharp\">\n" +
            "public int First;\n" +
            "</script>\n" +
            "<script setup lang=\"csharp\">\n" +
            "public int Second;\n" +
            "    \n" +
            "</script>\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var completions = service.GetCompletions(
            VueDocumentUri,
            new LanguagePosition(5, 4));

        completions.ShouldContain(item => item.Label == "First");
        completions.ShouldContain(item => item.Label == "Second");
    }

    [Fact]
    public void GetCompletions_ScaffoldOnSetupSnippet_IsOffered()
    {
        var completions = CompleteInScript("public int Counter;");

        completions.ShouldContain(item => item.Label == "OnSetup");
        completions.ShouldContain(item => item.Label == "Context");
    }

    [Fact]
    public void GetCompletions_ContextMemberAccess_IsUnaffectedByDeclaredMembers()
    {
        var completions = CompleteInScript(
            "public Reference<int> Counter = Reactive.Reference(0);",
            typedLine: "    Context.");

        completions.ShouldBeSameAs(ViuCompletionCatalog.ContextMembers);
    }

    [Fact]
    public void Read_UnchangedScriptText_ParsesOnlyOnce()
    {
        // Run-count pinning per .claude/rules/testing.md: the cache is what makes the design
        // incremental — a non-script edit recreates the document but leaves the script content
        // string equal, and must not reparse.
        var reader = new ScriptDeclarationReader();
        const string content = "\npublic int Counter;\n";

        var first = reader.Read(content);
        var second = reader.Read(content);

        reader.CacheMissCount.ShouldBe(1);
        second.ShouldBeSameAs(first);
        first.ShouldContain(member => member.Name == "Counter");
    }

    private static IReadOnlyList<LanguageCompletionItem> CompleteInScript(
        string scriptBody,
        string typedLine = "    ")
    {
        var source = $"@script {{\n{scriptBody}\n{typedLine}\n}}\n";
        var typedLineIndex = 1 + scriptBody.Count(character => character == '\n') + 1;
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);
        return service.GetCompletions(
            DocumentUri,
            new LanguagePosition(typedLineIndex, typedLine.Length));
    }
}
