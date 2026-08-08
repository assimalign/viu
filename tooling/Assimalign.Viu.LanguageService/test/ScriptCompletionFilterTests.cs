using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins prefix filtering inside an <c>@script</c> block. The service hosts no compilation, so this is
/// a lexical aid rather than C# IntelliSense — but answering a partially typed word with items that
/// cannot match it reads as the editor ignoring the keystroke entirely.
/// </summary>
public class ScriptCompletionFilterTests
{
    private const string DocumentUri = "file:///workspace/AppShell.viu";

    [Fact]
    public void GetCompletions_PartiallyTypedUsingKeyword_OffersUsingRatherThanScaffoldSnippets()
    {
        // The reported defect: typing `u` returned all six scaffold items - Context, Reactive,
        // "reactive reference", "computed value", "mounted callback", "async event handler" - none of
        // which start with `u`, and never offered `using`.
        var completions = CompleteAfter("    u");

        completions.ShouldContain(item => item.Label == "using");
        completions.ShouldAllBe(
            item => item.Label.StartsWith("u", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetCompletions_PartiallyTypedReactiveScaffold_StillOffersTheViuItem()
    {
        var completions = CompleteAfter("    Reac");

        completions.ShouldContain(item => item.Label == "Reactive");
    }

    [Fact]
    public void GetCompletions_EmptyScriptLine_OffersScaffoldAndKeywords()
    {
        var completions = CompleteAfter("    ");

        completions.ShouldContain(item => item.Label == "Context");
        completions.ShouldContain(item => item.Label == "using");
    }

    [Fact]
    public void GetCompletions_UnmatchedPrefix_FallsBackRatherThanReturningNothing()
    {
        // An unknown identifier should not produce an empty popup; the scaffold list is still useful.
        var completions = CompleteAfter("    zzz");

        completions.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetCompletions_ContextMemberAccess_IsUnaffectedByFiltering()
    {
        var completions = CompleteAfter("    Context.");

        completions.ShouldContain(item => item.Label == "Services");
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageCompletionItem> CompleteAfter(
        string scriptLine)
    {
        var source = $"@script {{\n{scriptLine}\n}}\n";
        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);
        return service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, scriptLine.Length));
    }
}
