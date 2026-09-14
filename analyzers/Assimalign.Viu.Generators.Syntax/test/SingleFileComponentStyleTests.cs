using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;
using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// End-to-end style diagnostics, ordinary CSS extraction, and incremental-cache coverage.
/// Scoped options are rejected by the canonical container and warned for compatibility input
/// under <c>[V01.01.06.17]</c>.
/// </summary>
public sealed class SingleFileComponentStyleTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Theory]
    [InlineData("<style scoped>.box { color: red; }</style>", 7)]
    [InlineData("@style scoped {\n    .box { color: red; }\n}\n", 7)]
    public void ScopedStyle_CanonicalInput_ReportsLocatedError(string source, int column)
    {
        // [V01.01.06.17] Both canonical container syntaxes reject the removed option.
        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);
        var diagnostic = outcome.Diagnostics.Where(item => item.Id == "VIU1001").ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1001");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("Scoped styles are not supported; Viu compiles component styles as ordinary global stylesheets. Remove the scoped option or use a CSS module.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(0);
        diagnostic.Location.GetLineSpan().StartLinePosition.Character.ShouldBe(column);
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldNotContain("data-v-");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void ScopedStyle_VueCompatibilityInput_WarnsAndEmitsOrdinaryCss(string serverRendering)
    {
        // [V01.01.06.17], [VUE-2]: preserve the compatibility block content with a located warning.
        const string source = "<template><div class=\"box\">hi</div></template>\n<style scoped>.box { color: red; }</style>";
        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.vue", source, RootNamespace, ProjectDirectory, serverRendering: serverRendering);
        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1002");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("Scoped styles are not supported; Viu compiles component styles as ordinary global stylesheets. Remove the scoped option or use a CSS module.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(1);
        diagnostic.Location.GetLineSpan().StartLinePosition.Character.ShouldBe(7);
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain(".box { color: red; }");
        generated.ShouldNotContain("data-v-");
    }

    [Fact]
    public void PlainStyle_PassesThroughUnmodified_AndEmitsNoScopeAttributes()
    {
        const string source =
            "<style>\n" +
            "    .box .inner { color: red; }\n" +
            "</style>\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Plain.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Plain.SingleFileComponent.g.cs");
        generated.ShouldNotContain("internal const string ScopeId");
        // Unmodified: the raw CSS content is emitted verbatim, with no [data-v-...] attribute injected.
        generated.ShouldContain("internal const string ExtractedStyles =");
        generated.ShouldNotContain("[data-v-");
        generated.ShouldContain(".box .inner { color: red; }");
    }

    [Fact]
    public void ComponentWithoutStyle_EmitsStyleSeamComment_NoConstants()
    {
        const string source = "<template>\n    <div>ok</div>\n</template>\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Bare.viu", source, RootNamespace, ProjectDirectory);

        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Bare.SingleFileComponent.g.cs");
        generated.ShouldContain("[V01.01.06.17] Style seam. This component declares no style block");
        generated.ShouldNotContain("internal const string ScopeId");
        generated.ShouldNotContain("internal const string ExtractedStyles");
    }

    [Fact]
    public void MalformedCss_SurfacesStyleDiagnostic_OnExactViuCoordinates()
    {
        // The style block is dispatched to the CSS parser (the registration seam). A declaration missing
        // its colon is MissingDeclarationColon; it maps to the VIU1301 style error composed onto the .viu
        // file — proving the seam parses the block content and routes CSS diagnostics through the style
        // origin envelope on exact coordinates.
        const string source =
            "<template>\n" +   // line 1
            "    <div>ok</div>\n" +  // line 2
            "</template>\n" +  // line 3
            "\n" +             // line 4
            "<style>\n" +  // line 5
            "    .a { color red; }\n" +  // line 6 — the CSS error line
            "</style>\n";      // line 7

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1301");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("(CSS code");
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe($"{ProjectDirectory}/Card.viu");
        span.StartLinePosition.Line.ShouldBe(5); // .viu file line 6, zero-based — the "color red" line
        // Recoverable: the scaffold is still emitted even though the CSS has an error.
        outcome.Sources.ShouldNotBeEmpty();
    }

    [Fact]
    public void PlainStyle_IdenticalInput_StaysStrictlyCached()
    {
        // The style compilation is deterministic and value-equatable, so it must not break the incremental
        // cache: an unchanged component leaves the model step strictly Cached.
        const string source =
            "<template>\n    <div>hi</div>\n</template>\n" +
            "<style>\n    .box { color: red; }\n</style>\n";

        var file = new InMemoryAdditionalText($"{ProjectDirectory}/Card.viu", source);
        var compilation = GeneratorTestHarness.CreateCompilation();
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(file), RootNamespace, ProjectDirectory);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        driver.GetRunResult().Results[0]
            .TrackedSteps[SingleFileComponentGenerator.ModelTrackingName]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

}
