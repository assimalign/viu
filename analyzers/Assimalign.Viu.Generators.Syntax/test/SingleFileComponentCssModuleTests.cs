using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;
using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// End-to-end tests for the CSS Modules and <c>v-bind()</c> generator emission ([V01.01.06.06], issue #62):
/// a <c>@style module</c> block produces the hashed CSS plus the typed <c>$style</c>-equivalent accessor,
/// a <c>v-bind()</c> block produces the custom-property CSS plus the <c>ApplyCssVariables</c> seam the
/// <c>UseCssVariables</c> runtime consumes, both compose with <c>scoped</c>, malformed <c>v-bind()</c> surfaces a
/// style diagnostic on the <c>.viu</c> coordinates, and the additions stay strictly cached. The rewrite
/// semantics themselves are pinned in the Css library's rewriter tests; these pin the generator wiring.
/// </summary>
public sealed class SingleFileComponentCssModuleTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void ModuleStyle_HashesClassNames_AndEmitsTypedAccessor()
    {
        const string source = "@style module {\n    .box { color: red; }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        // The default `module` maps to the `Style` accessor (the C# analogue of Vue's `$style`).
        generated.ShouldContain("internal static class Style");
        generated.ShouldContain("public const string box = \"box_");
        // The extracted CSS carries the hashed selector, not the original.
        generated.ShouldContain(".box_");
        generated.ShouldNotContain(".box {");
    }

    [Fact]
    public void ModuleStyle_HonorsCustomModuleName_AndSanitizesMembers()
    {
        const string source = "@style module=\"classes\" {\n    .my-box { color: red; }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        // module="classes" -> the `Classes` accessor; the hyphenated class becomes a valid C# member.
        generated.ShouldContain("internal static class Classes");
        generated.ShouldContain("public const string my_box = \"my-box_");
    }

    [Fact]
    public void VBindStyle_RewritesToCustomProperty_AndEmitsApplyCssVariablesSeam()
    {
        const string source =
            "@script {\n    public string color = \"red\";\n}\n" +
            "@style {\n    .a { color: v-bind(color); }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        // The CSS references a hashed custom property, and the seam wires the UseCssVariables runtime with a
        // getter that evaluates the original expression (resolved against the merged @script member `color`).
        generated.ShouldContain("color: var(--");
        generated.ShouldContain("internal void ApplyCssVariables()");
        generated.ShouldContain("global::Assimalign.Viu.Browser.CssVariables.UseCssVariables(Context,");
        generated.ShouldContain("(object?)(color)");
    }

    [Fact]
    public void ModuleStyle_TemplateStyleReference_ResolvesToAccessorClass()
    {
        // [V01.01.05.04.01] `:class="$style.box"` in the template resolves to the generated `Style` accessor
        // class, so the render body binds `Style.box` (a const) — not a phantom `_ctx.box` member.
        const string source =
            "@template {\n    <div :class=\"$style.box\">hi</div>\n}\n" +
            "@style module {\n    .box { color: red; }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain("Style.box");
        generated.ShouldContain("internal static class Style");
        generated.ShouldNotContain("_ctx.box");
    }

    [Fact]
    public void ModuleStyle_NamedTemplateReference_ResolvesToPascalCasedAccessorClass()
    {
        // [V01.01.05.04.01] A named module `<style module="theme">` is referenced by its authored name
        // `theme` in the template and resolves to the pascal-cased `Theme` accessor class.
        const string source =
            "@template {\n    <div :class=\"theme.active\">hi</div>\n}\n" +
            "@style module=\"theme\" {\n    .active { color: red; }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain("Theme.active");
        generated.ShouldContain("internal static class Theme");
    }

    [Fact]
    public void ModuleStyle_UnknownTemplateMember_SurfacesMappedDiagnostic()
    {
        // [V01.01.05.04.01] The generator supplies the complete class map, so a `$style.<missing>` reference is
        // reported on the .viu template coordinate (line 2), recoverable.
        const string source =
            "@template {\n    <div :class=\"$style.missing\">hi</div>\n}\n" +  // line 2 — the bad member
            "@style module {\n    .box { color: red; }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1101"); // a dispatched @template-compile diagnostic
        diagnostic.GetMessage().ShouldContain("'$style' has no member 'missing'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(1); // .viu file line 2, zero-based
        outcome.Sources.ShouldNotBeEmpty();
    }

    [Fact]
    public void VBind_ReferenceMember_UnwrapsToValue_InGetter()
    {
        // [V01.01.06.06.01] `v-bind(count)` with a script IReactiveReference<T> member unwraps to
        // `count.Value` in the
        // ApplyCssVariables getter — instance-member mode, so no `_ctx.` receiver — matching upstream cssVars
        // ergonomics instead of forcing `v-bind(count.Value)`.
        const string source =
            "@script {\n    public Reference<int> count;\n}\n" +
            "@style {\n    .a { width: v-bind(count); }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain("(object?)(count.Value)");
        generated.ShouldNotContain("count.Value.Value"); // not double-unwrapped
    }

    [Fact]
    public void VBind_NonReferenceMember_StaysBare_InGetter()
    {
        // A non-reference @script member is provably non-reactive, so it is read bare (no `.Value`, no unref).
        const string source =
            "@script {\n    public string color = \"red\";\n}\n" +
            "@style {\n    .a { color: v-bind(color); }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        generated.ShouldContain("(object?)(color)");
    }

    [Fact]
    public void VBind_MalformedExpression_SurfacesMappedStyleDiagnostic()
    {
        // [V01.01.06.06.01] A malformed C# v-bind expression is caught by the expression compile and surfaces a
        // diagnostic on the .viu style coordinate (line 2), recoverable.
        const string source =
            "@style {\n    .a { width: v-bind(1 +); }\n}\n"; // line 2 — the malformed expression

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1301"); // the style-origin envelope
        diagnostic.GetMessage().ShouldContain("Error parsing"); // the X_INVALID_EXPRESSION message prefix
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(1); // .viu file line 2, zero-based
        outcome.Sources.ShouldNotBeEmpty();
    }

    [Fact]
    public void ModuleAndVBind_ComposeWithScoped()
    {
        const string source =
            "@script {\n    public string c = \"red\";\n}\n" +
            "@style module scoped {\n    .box { color: v-bind(c); }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Card.SingleFileComponent.g.cs");
        // All three compose: the class is hashed, the scope attribute lands on it, and the value is a
        // custom property — plus the module accessor, the v-bind seam, and the scope id are all emitted.
        generated.ShouldContain("internal const string ScopeId = \"data-v-");
        generated.ShouldContain(".box_");
        generated.ShouldContain("[data-v-");
        generated.ShouldContain("color: var(--");
        generated.ShouldContain("internal static class Style");
        generated.ShouldContain("internal void ApplyCssVariables()");
    }

    [Fact]
    public void ComponentWithoutModuleOrVBind_EmitsNeitherSeam()
    {
        const string source = "@style {\n    .box { color: red; }\n}\n";

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Plain.viu", source, RootNamespace, ProjectDirectory);

        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Plain.SingleFileComponent.g.cs");
        generated.ShouldNotContain("internal static class Style");
        generated.ShouldNotContain("ApplyCssVariables");
        // A plain non-scoped block still passes through verbatim (unchanged [V01.01.06.04] behavior).
        generated.ShouldContain(".box { color: red; }");
    }

    [Fact]
    public void MalformedVBind_SurfacesStyleDiagnostic_OnExactViuCoordinates()
    {
        const string source =
            "@template {\n" +   // line 1
            "    <div>ok</div>\n" +  // line 2
            "}\n" +            // line 3
            "\n" +             // line 4
            "@style {\n" +     // line 5
            "    .a { color: v-bind(); }\n" +  // line 6 — the empty v-bind
            "}\n";             // line 7

        var outcome = GeneratorTestHarness.Run($"{ProjectDirectory}/Card.viu", source, RootNamespace, ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1301");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("(CSS code");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(5); // .viu file line 6, zero-based
        // Recoverable: the scaffold is still emitted.
        outcome.Sources.ShouldNotBeEmpty();
    }

    [Fact]
    public void ModuleTemplateReference_IdenticalInput_StaysStrictlyCached()
    {
        // [V01.01.05.04.01] The CSS module accessor threaded into the template compile is rebuilt from the
        // value-equatable module-class map, so an unchanged component still re-runs to an equal model — the
        // model step stays strictly Cached (not Unchanged, which would mean it re-executed and merely matched).
        const string source =
            "@template {\n    <div :class=\"$style.box\">hi</div>\n}\n" +
            "@style module {\n    .box { color: red; }\n}\n";

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

    [Fact]
    public void ModuleAndVBind_IdenticalInput_StaysStrictlyCached()
    {
        // The module/v-bind compilation is deterministic and value-equatable, so an unchanged component
        // leaves the model step strictly Cached — the incremental-generator contract.
        const string source =
            "@script {\n    public string c = \"red\";\n}\n" +
            "@style module {\n    .box { color: v-bind(c); }\n}\n";

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
