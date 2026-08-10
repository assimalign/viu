using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the completion context model for template markup. Utility completion activates only in static
/// class attributes and literal class-binding strings (docs/UTILITY-CSS-DESIGN.md section 10).
/// [V01.01.12.07.12] adds deliberately bounded C# completion for handler values and interpolations;
/// every other expression value stays suppressed so markup names cannot leak into it.
/// </summary>
public class TemplateExpressionContextTests
{
    private const string DocumentUri = "file:///workspace/AppShell.viu";

    [Fact]
    public void GetCompletions_BoundClassExpression_ReturnsNoAttributeNameCompletions()
    {
        // The reported defect: `:class="ShellClass"` answered with :class, :style, :key, :ref,
        // :disabled and :value while the caret sat inside the attribute value.
        var completions = CompleteAfter(
            "    <div :class=\"ShellClass\"></div>",
            "Shell");

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_EventHandlerExpression_OffersMethodsAndLambdaSnippetsOnly()
    {
        var completions = CompleteAfterWithScript(
            "    <button @click=\"Increment\"></button>",
            "Incre",
            "public int Count { get; set; }\npublic void Increment() { }");

        completions.ShouldContain(
            item => item.Label == "Increment" &&
                    item.Kind == LanguageCompletionItemKind.Method);
        var eventLambda = completions.Single(item => item.Label == "$event lambda");
        eventLambda.Kind.ShouldBe(LanguageCompletionItemKind.Snippet);
        eventLambda.IsSnippet.ShouldBeTrue();
        eventLambda.InsertText.ShouldBe("\\$event => $1");
        var asynchronousEventLambda = completions.Single(
            item => item.Label == "async $event lambda");
        asynchronousEventLambda.Kind.ShouldBe(LanguageCompletionItemKind.Snippet);
        asynchronousEventLambda.IsSnippet.ShouldBeTrue();
        asynchronousEventLambda.InsertText.ShouldBe("async \\$event => $1");
        completions.ShouldNotContain(item => item.Label == "Count");
    }

    [Fact]
    public void GetCompletions_DirectiveExpression_ReturnsNoCompletions()
        => CompleteAfter("    <div v-if=\"Visible\"></div>", "Visi").ShouldBeEmpty();

    [Fact]
    public void GetCompletions_OrdinaryAttributeValueContainingDirectiveText_ReturnsNoCompletions()
        => CompleteAfter("    <div title=\"see :class\"></div>", "see :cl").ShouldBeEmpty();

    [Fact]
    public void GetCompletions_TemplateInterpolation_OffersScriptMembers()
    {
        var completions = CompleteAfterWithScript(
            "    <p>{{ Count }}</p>",
            "{{ Cou",
            "public int Count { get; set; }\npublic void Increment() { }");

        completions.ShouldContain(
            item => item.Label == "Count" &&
                    item.Kind == LanguageCompletionItemKind.Property);
    }

    [Fact]
    public void GetCompletions_StaticClassAttribute_StillOffersUtilityCandidates()
    {
        // The gate must sit after the utility branch, or every working case regresses.
        var completions = CompleteAfter("    <div class=\"gap-\"></div>", "gap-");

        completions.ShouldContain(item => item.Label == "gap-4");
    }

    [Fact]
    public void GetCompletions_BoundClassQuotedLiteral_StillOffersUtilityCandidates()
    {
        var completions = CompleteAfter(
            "    <div :class=\"['bg-blue-', Extra]\"></div>",
            "'bg-blue-");

        completions.ShouldContain(item => item.Label == "bg-blue-500");
    }

    [Fact]
    public void GetHover_BoundClassExpression_ReturnsNull()
    {
        const string templateLine = "    <div :class=\"ShellClass\"></div>";
        var source = $"<template>\n{templateLine}\n</template>\n";
        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var hover = service.GetHover(
            DocumentUri,
            new LanguagePosition(1, templateLine.IndexOf("Shell", StringComparison.Ordinal) + 3));

        hover.ShouldBeNull();
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageCompletionItem> CompleteAfter(
        string templateLine,
        string typedPrefix)
    {
        var source = $"<template>\n{templateLine}\n</template>\n";
        var caret = templateLine.IndexOf(typedPrefix, StringComparison.Ordinal) + typedPrefix.Length;
        caret.ShouldBeGreaterThan(typedPrefix.Length - 1, "the probe text must occur in the line");

        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);
        return service.GetCompletions(DocumentUri, new LanguagePosition(1, caret));
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageCompletionItem> CompleteAfterWithScript(
        string templateLine,
        string typedPrefix,
        string scriptBody)
    {
        var source =
            $"<template>\n{templateLine}\n</template>\n" +
            $"@script {{\n{scriptBody}\n}}\n";
        var caret = templateLine.IndexOf(typedPrefix, StringComparison.Ordinal) + typedPrefix.Length;
        caret.ShouldBeGreaterThan(typedPrefix.Length - 1, "the probe text must occur in the line");

        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);
        return service.GetCompletions(DocumentUri, new LanguagePosition(1, caret));
    }
}
