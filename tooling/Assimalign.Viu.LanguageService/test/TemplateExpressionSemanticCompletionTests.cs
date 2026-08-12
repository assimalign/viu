using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins semantic completion inside template expressions ([V01.01.12.07.12]). A template expression is
/// compiled into the render body rather than merged verbatim, so these positions resolve through the
/// render source map's span directives — the only route by which a <c>v-for</c> alias, a local the
/// compiler declares over the loop source, has a type the editor can name at all.
/// </summary>
public class TemplateExpressionSemanticCompletionTests
{
    private const string Sibling =
        "namespace Test.App;\n" +
        "\n" +
        "public sealed class Capability\n" +
        "{\n" +
        "    public string Label { get; set; } = \"\";\n" +
        "    public string Status { get; set; } = \"\";\n" +
        "}\n";

    private const string Script =
        "@script {\n" +
        "using System.Collections.Generic;\n" +
        "\n" +
        "public IReadOnlyList<Capability> Capabilities { get; } = new List<Capability>();\n" +
        "public string CurrentPath { get; set; } = \"\";\n" +
        "public void GoBack() { }\n" +
        "}\n";

    [Fact]
    public void GetCompletions_ForAliasMemberAccess_OffersTheElementTypeMembers()
    {
        // The reported defect: the alias answered with the component's own members, or with nothing.
        // Its real type is the loop source's element type, which only the compiled loop knows.
        var completions = CompleteInLoopBody("    <h3>{{ capability. }}</h3>", "{{ capability.");

        var labels = completions.Select(item => item.Label).ToArray();
        labels.ShouldContain("Label");
        labels.ShouldContain("Status");
        labels.ShouldContain("ToString");
        labels.ShouldNotContain("CurrentPath");
        completions.Single(item => item.Label == "Label").Kind
            .ShouldBe(LanguageCompletionItemKind.Property);
    }

    [Fact]
    public void GetCompletions_ForAliasMemberAccessWithPartialName_FiltersToTheTypedPrefix()
    {
        // Inherited members are part of the type's surface: signal.ToString() completes like any other.
        var completions = CompleteInLoopBody("    <h3>{{ capability.To }}</h3>", "{{ capability.To");

        completions.Select(item => item.Label).ShouldBe(["ToString"]);
    }

    [Fact]
    public void GetCompletions_BindingValueMemberAccess_OffersTheReceiverMembers()
        => CompleteInLoopBody("    <h3 :title=\"capability.\"></h3>", ":title=\"capability.")
            .Select(item => item.Label)
            .ShouldContain("Label");

    [Fact]
    public void GetCompletions_ReactiveForSource_ResolvesThroughTheRenderGlue()
    {
        // A reactive source is unwrapped by the tier-three glue the build generates once per assembly.
        // The editor has no generator run, so the engine adds that same glue to its compilation; without
        // it the unwrap call binds to an error type and the alias has no members to offer.
        const string source =
            "<template>\n" +
            "  <article v-for=\"capability in Live\">\n" +
            "    <h3>{{ capability. }}</h3>\n" +
            "  </article>\n" +
            "</template>\n" +
            "@script {\n" +
            "using System.Collections.Generic;\n" +
            "using Assimalign.Viu.Reactivity;\n" +
            "\n" +
            "public Reference<IReadOnlyList<Capability>> Live =\n" +
            "    Reactive.Reference<IReadOnlyList<Capability>>(new List<Capability>());\n" +
            "}\n";

        Complete(source, 2, "    <h3>{{ capability.".Length)
            .Select(item => item.Label)
            .ShouldContain("Label");
    }

    [Fact]
    public void GetCompletions_EventHandlerValue_KeepsTheLambdaSnippetsBesideTheBoundMembers()
    {
        // A handler slot takes a member OR an inline lambda, and no symbol lookup can offer the
        // second. Binding the position must not cost the author the snippets it cannot produce.
        const string templateLine = "  <button @click=\"Go\"></button>";
        var source = $"<template>\n{templateLine}\n</template>\n{Script}";

        var labels = Complete(source, 1, templateLine.IndexOf("Go\"", StringComparison.Ordinal) + 2)
            .Select(item => item.Label)
            .ToArray();

        labels.ShouldContain("GoBack");
        labels.ShouldContain("$event lambda");
    }

    [Fact]
    public void GetCompletions_ForSourceAfterInKeyword_OffersComponentMembers()
    {
        const string templateLine = "  <article v-for=\"capability in Cap\"></article>";
        var source = $"<template>\n{templateLine}\n</template>\n{Script}";

        Complete(source, 1, templateLine.IndexOf("Cap\"", StringComparison.Ordinal) + "Cap".Length)
            .Select(item => item.Label)
            .ShouldContain("Capabilities");
    }

    [Fact]
    public void GetCompletions_EmptyForSource_FallsBackToTheDeclaredMembers()
    {
        // An empty source is a malformed v-for the template compiler drops, so there is no compiled
        // expression to bind. The file's own declared members remain an honest degraded answer.
        const string templateLine = "  <article v-for=\"capability in \"></article>";
        var source = $"<template>\n{templateLine}\n</template>\n{Script}";

        var labels = Complete(source, 1, templateLine.IndexOf("in \"", StringComparison.Ordinal) + 3)
            .Select(item => item.Label)
            .ToArray();

        labels.ShouldContain("Capabilities");
        labels.ShouldContain("GoBack");
    }

    [Fact]
    public void GetCompletions_ForAliasDeclaration_OffersNothing()
    {
        // Before the in keyword the author is naming a new alias, not referring to anything.
        const string templateLine = "  <article v-for=\"cap\"></article>";
        var source = $"<template>\n{templateLine}\n</template>\n{Script}";

        Complete(source, 1, templateLine.IndexOf("cap\"", StringComparison.Ordinal) + "cap".Length)
            .ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_UnboundReceiverMemberAccess_OffersNothing()
    {
        // A receiver that does not bind has no members, and the component's do not stand in for them.
        const string templateLine = "  <h3>{{ missing.Value. }}</h3>";
        var source = $"<template>\n{templateLine}\n</template>\n{Script}";

        Complete(source, 1, templateLine.IndexOf("missing.Value.", StringComparison.Ordinal) +
                "missing.Value.".Length)
            .ShouldBeEmpty();
    }

    private static IReadOnlyList<LanguageCompletionItem> CompleteInLoopBody(
        string bodyLine,
        string probe)
    {
        var source =
            "<template>\n" +
            "  <article v-for=\"capability in Capabilities\">\n" +
            $"{bodyLine}\n" +
            "  </article>\n" +
            "</template>\n" +
            Script;
        return Complete(
            source,
            2,
            bodyLine.IndexOf(probe, StringComparison.Ordinal) + probe.Length);
    }

    private static IReadOnlyList<LanguageCompletionItem> Complete(
        string source,
        int line,
        int character)
    {
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(
                ScriptSemanticFixture.DocumentUri,
                ScriptSemanticFixture.CreateContext(
                    new LanguageProjectSourceDocument(
                        "C:/workspace/App/Capability.cs",
                        Sibling,
                        IsComponent: false)));
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        return service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(line, character));
    }
}
