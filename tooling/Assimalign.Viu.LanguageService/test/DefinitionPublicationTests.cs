using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins where Go To Definition lands ([V01.01.12.07.16]). Every reported position is an authored one:
/// a component's member is declared in that component's generated document as far as the compiler is
/// concerned, and navigating there would open a file the author never wrote.
/// </summary>
public class DefinitionPublicationTests
{
    private const string ButtonComponentSource =
        "<template>\n" +
        "  <button>{{ Label }}</button>\n" +
        "</template>\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Parameter(IsRequired = true)]\n" +
        "    public string Label { get; set; } = string.Empty;\n" +
        "}\n";

    private const string SiblingSource =
        "namespace Test.App;\n" +
        "\n" +
        "public sealed class Capability\n" +
        "{\n" +
        "    public string Label { get; set; } = \"\";\n" +
        "}\n";

    [Fact]
    public void GetDefinition_ScriptMemberReference_LandsOnItsDeclarationInTheSameFile()
    {
        const string source =
            "@script {\n" +
            "    public int Count { get; set; }\n" +
            "    public int Read() => Count;\n" +
            "}\n";
        var declarationStart = "    public int ".Length;
        var referenceStart = "    public int Read() => ".Length;

        var location = Define(source, 2, referenceStart + 2).ShouldHaveSingleItem();

        location.FilePath.ShouldBe(ScriptSemanticFixture.DocumentFilePath);
        location.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(1, declarationStart),
                new LanguagePosition(1, declarationStart + "Count".Length)));
    }

    [Fact]
    public void GetDefinition_TemplateExpression_LandsOnTheScriptDeclaration()
    {
        // The expression only exists in the compiled render body, so this is the same route the
        // template hover takes: bind where the compiler put it, report where the author wrote it.
        const string source =
            "<template>\n" +
            "  <p>{{ CurrentPath }}</p>\n" +
            "</template>\n" +
            "@script {\n" +
            "    public string CurrentPath { get; set; } = \"\";\n" +
            "}\n";
        var declarationStart = "    public string ".Length;

        var location = Define(source, 1, "  <p>{{ ".Length + 2).ShouldHaveSingleItem();

        location.FilePath.ShouldBe(ScriptSemanticFixture.DocumentFilePath);
        location.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(4, declarationStart),
                new LanguagePosition(4, declarationStart + "CurrentPath".Length)));
    }

    [Fact]
    public void GetDefinition_ComponentTypeReference_LandsInThatComponentsOwnFile()
    {
        // A sibling component is projected too, so its declaration carries the same #line map back to
        // the .viu the author edits rather than to the C# the generator emitted for it.
        const string source =
            "@script {\n" +
            "    public Button Card { get; set; } = new();\n" +
            "}\n";

        var location = Define(
                source,
                1,
                "    public Bu".Length,
                new LanguageProjectSourceDocument(
                    "C:/workspace/App/Button.viu",
                    ButtonComponentSource,
                    IsComponent: true))
            .ShouldHaveSingleItem();

        // The component's own class is generator scaffold with no authored name to land on, so the
        // file itself is the answer: a .viu file is where the author declared the component.
        location.FilePath.ShouldBe("C:/workspace/App/Button.viu");
        location.Range.ShouldBe(
            new LanguageRange(new LanguagePosition(0, 0), new LanguagePosition(0, 0)));
    }

    [Fact]
    public void GetDefinition_ForAliasMember_LandsOnTheElementTypesDeclaration()
    {
        // The alias has a type only inside the compiled loop, and its expression reaches the render
        // body verbatim, so the whole member access maps and not merely its root.
        const string source =
            "<template>\n" +
            "  <article v-for=\"item in Items\">{{ item.Label }}</article>\n" +
            "</template>\n" +
            "@script {\n" +
            "using System.Collections.Generic;\n" +
            "\n" +
            "public IReadOnlyList<Capability> Items { get; } = new List<Capability>();\n" +
            "}\n";
        var declarationStart = "    public string ".Length;

        var location = Define(
                source,
                1,
                "  <article v-for=\"item in Items\">{{ item.".Length + 2,
                new LanguageProjectSourceDocument(
                    "C:/workspace/App/Capability.cs",
                    SiblingSource,
                    IsComponent: false))
            .ShouldHaveSingleItem();

        location.FilePath.ShouldBe("C:/workspace/App/Capability.cs");
        location.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(4, declarationStart),
                new LanguagePosition(4, declarationStart + "Label".Length)));
    }

    [Fact]
    public void GetDefinition_TypeDeclaredInAPlainSibling_LandsInThatFileUntranslated()
    {
        // A plain C# sibling was never projected: its tree is the authored file, so its span needs no
        // translation at all.
        const string source =
            "@script {\n" +
            "    public Capability Item { get; set; } = new();\n" +
            "}\n";

        var location = Define(
                source,
                1,
                "    public Cap".Length,
                new LanguageProjectSourceDocument(
                    "C:/workspace/App/Capability.cs",
                    SiblingSource,
                    IsComponent: false))
            .ShouldHaveSingleItem();

        location.FilePath.ShouldBe("C:/workspace/App/Capability.cs");
        location.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(2, "public sealed class ".Length),
                new LanguagePosition(2, "public sealed class ".Length + "Capability".Length)));
    }

    [Fact]
    public void GetDefinition_SymbolFromMetadata_ReportsNothingToNavigateTo()
    {
        // A reference-assembly symbol has no source to open, and inventing one would be worse than
        // the editor saying it found nothing.
        const string source =
            "@script {\n" +
            "    public string Text { get; set; } = \"\";\n" +
            "}\n";

        Define(source, 1, "    public str".Length).ShouldBeEmpty();
    }

    [Fact]
    public void GetDefinition_MarkupRatherThanCode_ReportsNothing()
    {
        const string source =
            "<template>\n" +
            "  <section></section>\n" +
            "</template>\n";

        Define(source, 1, "  <sec".Length).ShouldBeEmpty();
    }

    [Fact]
    public void GetDefinition_WithoutAProjectContext_ReportsNothing()
    {
        const string source =
            "@script {\n" +
            "    public int Count { get; set; }\n" +
            "    public int Read() => Count;\n" +
            "}\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        service.GetDefinition(
                ScriptSemanticFixture.DocumentUri,
                new LanguagePosition(2, "    public int Read() => ".Length + 2))
            .ShouldBeEmpty();
    }

    private static System.Collections.Generic.IReadOnlyList<LanguageLocation> Define(
        string source,
        int line,
        int character,
        params LanguageProjectSourceDocument[] siblings)
    {
        var service = LanguageServices.Create();
        service.ShouldBeAssignableTo<IScriptSemanticLanguageService>()
            .ConfigureProjectContext(
                ScriptSemanticFixture.DocumentUri,
                ScriptSemanticFixture.CreateContext(siblings));
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        return service.GetDefinition(
            ScriptSemanticFixture.DocumentUri,
            new LanguagePosition(line, character));
    }
}
