using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Tooling.SingleFileComponent;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// End-to-end generator coverage for [V01.01.06.09] tag-based <c>.vue</c> compatibility.
/// </summary>
public sealed class VueSingleFileComponentGeneratorTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void Generate_TagBasedBlocks_CompilesTemplateCSharpScriptScopedStyleAndCssModule()
    {
        const string source =
            "<template>\n" +
            "<div :class=\"theme.active\">{{ Count }}</div>\n" +
            "</template>\n" +
            "<script lang=\"csharp\">\n" +
            "public int Count = 1;\n" +
            "</script>\n" +
            "<style scoped>\n" +
            ".card { color: red; }\n" +
            "</style>\n" +
            "<style module=\"theme\">\n" +
            ".active { font-weight: bold; }\n" +
            "</style>\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Card.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain("\"Card.vue\"");
        generated.ShouldContain("internal static object? Render(");
        generated.ShouldContain("Theme.active");
        generated.ShouldContain("public int Count = 1;");
        generated.ShouldContain($"#line 4 \"{ProjectDirectory}/Card.vue\"");
        generated.ShouldContain(".card[data-v-");
        generated.ShouldContain("internal static class Theme");
        generated.ShouldContain("public const string active = \"active_");
    }

    [Fact]
    public void Generate_InlineTemplate_MapsRenderExpressionToExactVueSpan()
    {
        const string source = "<template><div>{{ missing }}</div></template>";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Inline.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Inline.SingleFileComponent.g.cs");
        generated.ShouldContain(
            $"#line (1,19)-(1,26) 88 \"{ProjectDirectory}/Inline.vue\"");
    }

    [Fact]
    public void Generate_InlineMalformedCSharpScript_MapsDiagnosticAndEmissionColumnToVueSource()
    {
        const string source = "<script lang=\"csharp\">public int Value = ;</script>";
        var contentStart = source.IndexOf("public", System.StringComparison.Ordinal);
        var errorOffset = source.IndexOf(';');

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/InlineScript.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.Single(candidate => candidate.Id == "VIU1201");
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe($"{ProjectDirectory}/InlineScript.vue");
        span.StartLinePosition.Line.ShouldBe(0);
        span.StartLinePosition.Character.ShouldBe(errorOffset);

        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "InlineScript.SingleFileComponent.g.cs");
        generated.ShouldContain(
            $"#line 1 \"{ProjectDirectory}/InlineScript.vue\"\n" +
            new string(' ', contentStart) +
            "public int Value = ;");
    }

    [Fact]
    public void Generate_ScriptWithoutLanguage_ReportsLocatedUnsupportedLanguageAndDoesNotMerge()
    {
        const string source = "<script>public int Count;</script>";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/ImplicitScript.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1206");
        diagnostic.Location.SourceSpan.Start.ShouldBe(0);
        diagnostic.Location.SourceSpan.End.ShouldBe("<script>".Length);
        diagnostic.GetMessage().ShouldContain("lang=\"csharp\"");
        diagnostic.GetMessage().ShouldContain("never executes JavaScript");

        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "ImplicitScript.SingleFileComponent.g.cs");
        generated.ShouldNotContain("public int Count;");
    }

    [Fact]
    public void Generate_JavaScriptLanguage_ReportsOnLanguageAttributeAndDoesNotMerge()
    {
        const string source = "<script lang=\"javascript\">export default {};</script>";
        const string attribute = "lang=\"javascript\"";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/JavaScript.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1206");
        diagnostic.Location.SourceSpan.Start.ShouldBe(source.IndexOf(attribute, System.StringComparison.Ordinal));
        diagnostic.Location.SourceSpan.Length.ShouldBe(attribute.Length);
        GeneratorTestHarness.GeneratedSource(outcome, "JavaScript.SingleFileComponent.g.cs")
            .ShouldNotContain("export default");
    }

    [Fact]
    public void Generate_OrdinaryAndSetupCSharpScripts_MergesBothWithExactSourceMaps()
    {
        const string source =
            "<template><div>{{ Ordinary }} {{ SetupCount }}</div></template>\n" +
            "<script lang=\"csharp\">public int Ordinary = 1;</script>\n" +
            "<script setup lang=\"csharp\">using System;\n" +
            "public int SetupCount = Math.Max(2, 1);</script>";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Setup.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Setup.SingleFileComponent.g.cs");
        generated.ShouldContain("public int Ordinary = 1;");
        generated.ShouldContain("using System;");
        generated.ShouldContain("public int SetupCount = Math.Max(2, 1);");
        generated.ShouldContain($"#line 2 \"{ProjectDirectory}/Setup.vue\"");
        generated.ShouldContain($"#line 3 \"{ProjectDirectory}/Setup.vue\"");
        generated.ShouldContain($"#line 4 \"{ProjectDirectory}/Setup.vue\"");
        generated.ShouldContain("_ctx.Ordinary");
        generated.ShouldContain("_ctx.SetupCount");
    }

    [Fact]
    public void Generate_MalformedSetupCSharpScript_MapsDiagnosticToExactVueSpan()
    {
        const string source =
            "<script setup lang=\"csharp\">public int Count = ;</script>";
        var errorOffset = source.IndexOf(';');

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/MalformedSetup.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.Single(candidate => candidate.Id == "VIU1201");
        diagnostic.Location.SourceSpan.Start.ShouldBe(errorOffset);
        diagnostic.Location.GetLineSpan().Path.ShouldBe(
            $"{ProjectDirectory}/MalformedSetup.vue");
        GeneratorTestHarness.GeneratedSource(
                outcome,
                "MalformedSetup.SingleFileComponent.g.cs")
            .ShouldContain("public int Count = ;");
    }

    [Fact]
    public void Generate_SetupBeforeOrdinaryScript_PreservesObservableMemberOrder()
    {
        const string source =
            "<script setup lang=\"csharp\">public int First = 1;</script>\n" +
            "<script lang=\"csharp\">public int Second = First;</script>";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Ordered.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Ordered.SingleFileComponent.g.cs");
        generated.IndexOf("public int First", System.StringComparison.Ordinal)
            .ShouldBeLessThan(
                generated.IndexOf("public int Second", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_MalformedTagContainer_ReportsRecoverableDiagnosticOnVueFile()
    {
        const string source = "<template><div></template";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Malformed.vue",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "VIU1001");
        outcome.Diagnostics
            .Where(diagnostic => diagnostic.Id == "VIU1001")
            .ShouldAllBe(diagnostic =>
                diagnostic.Location.GetLineSpan().Path == $"{ProjectDirectory}/Malformed.vue");
        outcome.Sources.ShouldNotBeEmpty();
    }

    [Fact]
    public void Generate_SameBaseViuAndVue_CanonicalViuWinsAndVueReportsCollision()
    {
        const string viuSource = "<template>\n<div>canonical</div>\n</template>\n";
        const string vueSource = "<template><div>compatibility</div></template>";
        var viu = new InMemoryAdditionalText($"{ProjectDirectory}/Card.viu", viuSource);
        var vue = new InMemoryAdditionalText($"{ProjectDirectory}/Card.vue", vueSource);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(vue, viu),
            RootNamespace,
            ProjectDirectory);

        driver = driver.RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        result.GeneratedSources.Length.ShouldBe(1);
        result.GeneratedSources[0].SourceText.ToString().ShouldContain("\"Card.viu\"");
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1004");
        diagnostic.Location.GetLineSpan().Path.ShouldBe($"{ProjectDirectory}/Card.vue");
    }

    [Fact]
    public void Resolve_VueName_StripsCompatibilityExtension()
    {
        var resolved = SingleFileComponentNameResolver.Resolve(
            $"{ProjectDirectory}/Components/Card.vue",
            ProjectDirectory,
            RootNamespace);

        resolved.ClassName.ShouldBe("Card");
        resolved.Namespace.ShouldBe("Demo.Components");
        resolved.HintName.ShouldBe("Components.Card.SingleFileComponent.g.cs");
    }

    [Fact]
    public void Generate_IdenticalVueInput_ReusesCachedModel()
    {
        var file = new InMemoryAdditionalText(
            $"{ProjectDirectory}/Cached.vue",
            "<template><div>cached</div></template>");
        var compilation = GeneratorTestHarness.CreateCompilation();
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(file),
            RootNamespace,
            ProjectDirectory);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        driver.GetRunResult().Results[0]
            .TrackedSteps[SingleFileComponentGenerator.ModelTrackingName]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }
}
