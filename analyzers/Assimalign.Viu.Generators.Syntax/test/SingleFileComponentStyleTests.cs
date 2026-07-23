using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;
using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Generators.Tests;

/// <summary>
/// End-to-end tests for the <c>@style</c> registration seam ([V01.01.06.04]): the composition root routes
/// <c>@style</c> block content to the CSS parser, scoped blocks are rewritten with the component's stable
/// <c>data-v-&lt;hash&gt;</c> scope id and surface as the <c>ScopeId</c>/<c>ExtractedStyles</c> constants,
/// non-scoped blocks pass through unmodified, and CSS parse diagnostics flow through the style-origin
/// envelope onto exact <c>.viu</c> coordinates. The scoped-selector semantics themselves are pinned in the
/// Css library's <c>CssScopedRewriterTests</c> against upstream; these tests pin the generator wiring.
/// </summary>
public sealed class SingleFileComponentStyleTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void ScopedStyle_EmitsScopeIdAndRewrittenCss()
    {
        const string source =
            "@template {\n" +
            "    <div class=\"box\">hi</div>\n" +
            "}\n" +
            "\n" +
            "@style scoped {\n" +
            "    .box .inner { color: red; }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain("internal const string ScopeId = \"data-v-");
        // The scoped rewrite lands the attribute on the last compound only (upstream scoping parity).
        generated.ShouldContain(".box .inner[data-v-");
        generated.ShouldContain("internal const string ExtractedStyles =");
    }

    [Fact]
    public void ScopeId_IsStableAcrossRuns_ForTheSamePath()
    {
        const string source = "@style scoped {\n    .a { color: red; }\n}\n";

        var first = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);
        var second = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        var firstScope = ScopeIdOf(GeneratorTestHarness.GeneratedSource(first, "Card.SingleFileComponent.g.cs"));
        var secondScope = ScopeIdOf(GeneratorTestHarness.GeneratedSource(second, "Card.SingleFileComponent.g.cs"));
        firstScope.ShouldNotBeNull();
        // Deterministic and path-based: the same .viu path yields the same scope id (asset-caching contract).
        secondScope.ShouldBe(firstScope);
    }

    [Fact]
    public void ScopeId_DiffersByComponentPath()
    {
        const string source = "@style scoped {\n    .a { color: red; }\n}\n";

        var a = GeneratorTestHarness.Run($"{ProjectDirectory}/A.viu", source, RootNamespace, ProjectDirectory);
        var b = GeneratorTestHarness.Run($"{ProjectDirectory}/B.viu", source, RootNamespace, ProjectDirectory);

        ScopeIdOf(GeneratorTestHarness.GeneratedSource(a, "A.SingleFileComponent.g.cs"))
            .ShouldNotBe(ScopeIdOf(GeneratorTestHarness.GeneratedSource(b, "B.SingleFileComponent.g.cs")));
    }

    [Fact]
    public void NonScopedStyle_PassesThroughUnmodified_AndEmitsNoScopeId()
    {
        const string source =
            "@style {\n" +
            "    .box .inner { color: red; }\n" +
            "}\n";

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
        const string source = "@template {\n    <div>ok</div>\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Bare.viu", source, RootNamespace, ProjectDirectory);

        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Bare.SingleFileComponent.g.cs");
        generated.ShouldContain("[V01.01.06.04] Style seam. This component declares no @style block");
        generated.ShouldNotContain("internal const string ScopeId");
        generated.ShouldNotContain("internal const string ExtractedStyles");
    }

    [Fact]
    public void MalformedCss_SurfacesStyleDiagnostic_OnExactViuCoordinates()
    {
        // The @style block is dispatched to the CSS parser (the registration seam). A declaration missing
        // its colon is MissingDeclarationColon; it maps to the VIU1301 style error composed onto the .viu
        // file — proving the seam parses the block content and routes CSS diagnostics through the style
        // origin envelope on exact coordinates.
        const string source =
            "@template {\n" +   // line 1
            "    <div>ok</div>\n" +  // line 2
            "}\n" +            // line 3
            "\n" +             // line 4
            "@style scoped {\n" +  // line 5
            "    .a { color red; }\n" +  // line 6 — the CSS error line
            "}\n";             // line 7

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
    public void ScopedStyle_IdenticalInput_StaysStrictlyCached()
    {
        // The style compilation is deterministic and value-equatable, so it must not break the incremental
        // cache: an unchanged component leaves the model step strictly Cached.
        const string source =
            "@template {\n    <div>hi</div>\n}\n" +
            "@style scoped {\n    .box { color: red; }\n}\n";

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

    private static string? ScopeIdOf(string generated)
    {
        const string marker = "internal const string ScopeId = \"";
        var start = generated.IndexOf(marker, System.StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = generated.IndexOf('"', start);
        return generated.Substring(start, end - start);
    }
}
