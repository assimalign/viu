using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;
using Xunit;

using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Tests for build-time component-usage validation ([SFC-USE-1]–[SFC-USE-5]): a template that uses a
/// component is checked against that component's declared parameter surface, whether the component is
/// compiled in the same compilation or read from a referenced assembly's metadata. The suite is
/// deliberately weighted toward the <em>silent</em> cases, because a false positive here breaks builds
/// that were previously correct while a false negative merely fails to help.
/// </summary>
public sealed class SingleFileComponentUsageValidationTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    // The declaring component every in-compilation case validates against: one required string, one
    // optional int, and an event, so the listener spellings are exercised alongside the parameters.
    private const string FeatureCard =
        "<template>\n" +
        "    <article><h2>{{ Title }}</h2></article>\n" +
        "</template>\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Parameter(IsRequired = true)] public string Title { get; set; } = \"\";\n" +
        "    [Parameter] public int Rating { get; set; }\n" +
        "\n" +
        "    [Event] public partial void Changed(int rating);\n" +
        "}\n";

    [Fact]
    public void CleanUsage_OfAnAttributedComponent_ReportsNothing()
    {
        // The positive case: every supplied name is declared, the required parameter is present, and the
        // literal types line up. A clean template must stay completely silent.
        Validate("<FeatureCard title=\"Compiled\" :rating=\"4\" @changed=\"OnChanged\" />")
            .ShouldBeEmpty();
    }

    [Fact]
    public void UnknownAttribute_IsAWarning_NotAnError()
    {
        // [SFC-USE-2] An undeclared attribute is LEGAL — it falls through to the component's rendered root
        // [CMP-17] — so the diagnostic reports a likely mistake at Warning severity. Making it an error
        // would contradict the fallthrough clause.
        var diagnostic = Validate("<FeatureCard title=\"Compiled\" unknown=\"y\" />")
            .ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("VIU1401");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("'unknown'");
        diagnostic.GetMessage().ShouldContain("'title'"); // the message lists what IS declared
        diagnostic.Location.GetLineSpan().Path.ShouldBe($"{ProjectDirectory}/OverviewView.viu");
    }

    [Fact]
    public void MissingRequiredParameter_IsAnError()
    {
        // [SFC-USE-3] Unlike an undeclared attribute there is no legitimate reading of the omission: the
        // declaration says the caller must supply it.
        var diagnostic = Validate("<FeatureCard :rating=\"4\" />").ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("VIU1402");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'title'");
        // The squiggle covers the tag name alone, not the element and everything nested inside it.
        var span = diagnostic.Location.GetLineSpan();
        span.StartLinePosition.Line.ShouldBe(1);       // .viu file line 2, zero-based
        span.StartLinePosition.Character.ShouldBe(5);  // just past the opening '<' at column 5
        span.EndLinePosition.Character.ShouldBe(5 + "FeatureCard".Length);
    }

    [Fact]
    public void PlainAttribute_SuppliedToAValueTypeParameter_IsAnError()
    {
        // [SFC-USE-4] A plain attribute's value is always a string, so `rating="4"` can never reach an
        // int parameter: Get<int> would yield 0. Decidable from the source alone, hence an error, and the
        // message names the fix.
        var diagnostic = Validate("<FeatureCard title=\"Compiled\" rating=\"4\" />")
            .ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("VIU1403");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain(":rating=");
    }

    [Fact]
    public void NonStringLiteralBinding_SuppliedToAStringParameter_IsAnError()
    {
        // [SFC-USE-4] The other decidable direction.
        var diagnostic = Validate("<FeatureCard :title=\"4\" />").ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("VIU1403");
        diagnostic.GetMessage().ShouldContain("'title'");
    }

    [Theory]
    // [SFC-USE-2] Everything a developer legitimately writes on a component: fallthrough attributes,
    // ARIA/data namespacing, the render pipeline's own names, listeners, and directives.
    [InlineData("<FeatureCard title=\"x\" class=\"wide\" style=\"color:red\" id=\"a\" role=\"note\" />")]
    [InlineData("<FeatureCard title=\"x\" data-test=\"a\" aria-label=\"b\" />")]
    [InlineData("<FeatureCard title=\"x\" :key=\"1\" ref=\"card\" tabindex=\"0\" />")]
    [InlineData("<FeatureCard title=\"x\" @click=\"OnClick\" @changed=\"OnChanged\" onMouseOver=\"h\" />")]
    [InlineData("<FeatureCard title=\"x\" v-if=\"Show\" v-once />")]
    public void LegitimateNonParameterProperties_AreSilent(string usage)
        => Validate(usage).ShouldBeEmpty();

    [Fact]
    public void ArgumentSpread_SilencesTheEntireUsage()
    {
        // [SFC-USE-5] `v-bind="object"` can supply any parameter, including the required one. Nothing
        // about the supplied set is decidable, so the checker reports neither the unknown attribute nor
        // the apparently missing required parameter.
        Validate("<FeatureCard v-bind=\"CardArguments\" unknown=\"y\" />").ShouldBeEmpty();
    }

    [Fact]
    public void DynamicArgumentName_SilencesTheEntireUsage()
    {
        // [SFC-USE-5] `:[name]="value"` names a parameter only at run time.
        Validate("<FeatureCard :[Key]=\"Value\" />").ShouldBeEmpty();
    }

    [Fact]
    public void NonLiteralBoundExpression_IsNotTypeChecked()
    {
        // [SFC-USE-4] The projection keeps expression bodies opaque and has no semantic model for them,
        // so a bound member reference is never type-checked — deliberately, because guessing would be a
        // false positive.
        Validate("<FeatureCard :title=\"Rating\" :rating=\"Title\" />").ShouldBeEmpty();
    }

    [Fact]
    public void ParameterlessGeneratedComponent_IsStillResolvable()
    {
        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/EmptyCard.viu",
                    "<template>\n" +
                    "    <article></article>\n" +
                    "</template>\n"),
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <EmptyCard />\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void UnresolvedStaticComponent_IsAnActionableError()
    {
        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <SomeoneElsesWidget v-bind=\"Arguments\" />\n" +
                    "</template>\n" +
                    "@script {\n" +
                    "    public object Arguments = new object();\n" +
                    "}\n"),
            ],
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1404");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("generated or in-scope declaration");
        diagnostic.GetMessage().ShouldContain("<component :is=");
    }

    [Theory]
    [InlineData("component")]
    [InlineData("Component")]
    public void RuntimeSelectedComponent_BothGenericTagSpellings_AreNotStaticallyResolved(
        string tag)
    {
        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <" + tag + " :is=\"Target\" />\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("viu")]
    [InlineData("vue")]
    public void VueIsCast_BothContainerFormats_AreNotStaticallyResolved(
        string extension)
    {
        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Consumer.{extension}",
                    "<template>\n" +
                    "    <div is=\"vue:runtime-widget\"></div>\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ImperativeParameterSurface_ResolvesIdentityButSkipsParameterChecks()
    {
        const string imperativeCard =
            "<template>\n" +
            "    <article></article>\n" +
            "</template>\n" +
            "@script {\n" +
            "    public object Parameters { get; } = new object();\n" +
            "}\n";

        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/ImperativeCard.viu", imperativeCard),
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <ImperativeCard unknown=\"y\" />\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void AmbiguousComponentName_IsAnActionableError()
    {
        // [SFC-USE-5] Two components answer to `Card`; the template's intent is undecidable and
        // validating against the wrong one would be a false positive, so neither is used.
        const string card =
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    using Assimalign.Viu.Components;\n" +
            "\n" +
            "    [Parameter(IsRequired = true)] public string Title { get; set; } = \"\";\n" +
            "}\n";

        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Shared/Card.viu", card),
                ($"{ProjectDirectory}/Views/Card.viu", card),
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <Card unknown=\"y\" />\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1405");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("more than one in-scope declaration");
        diagnostic.GetMessage().ShouldContain("exactly one target");
    }

    [Fact]
    public void ComponentFromAReferencedAssembly_IsValidatedThroughMetadata()
    {
        // [SFC-USE-1] The payoff of the attribute form: `[Parameter]` survives into metadata, so a
        // packaged component's input surface is readable by a consumer's template compiler. An imperative
        // declaration would carry nothing here.
        var reference = CompilePackagedComponent();

        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <PackagedCard unknown=\"y\" />\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory,
            reference);

        var byId = outcome.Diagnostics.ToLookup(diagnostic => diagnostic.Id);
        byId["VIU1401"].ShouldHaveSingleItem().GetMessage().ShouldContain("'unknown'");
        byId["VIU1402"].ShouldHaveSingleItem().GetMessage().ShouldContain("'title'");
    }

    [Fact]
    public void ComponentFromAReferencedAssembly_CleanUsage_IsSilent()
    {
        var reference = CompilePackagedComponent();

        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    <PackagedCard title=\"From metadata\" :rating=\"5\" />\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory,
            reference);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("<EmptyPackagedCard />")]
    [InlineData("<PackagedImperative unknown=\"y\" />")]
    public void ReferencedParameterlessOrImperativeComponent_ResolvesWithoutFalseParameterDiagnostics(
        string usage)
    {
        var reference = CompilePackagedComponent();

        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/Consumer.viu",
                    "<template>\n" +
                    "    " + usage + "\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory,
            reference);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    // Runs the declaring FeatureCard plus a consumer template that writes `usage`, and returns the
    // diagnostics. The declaring component itself is well-formed, so every diagnostic comes from the
    // usage under test.
    private static IReadOnlyList<RoslynDiagnostic> Validate(string usage)
    {
        var outcome = GeneratorTestHarness.RunAll(
            [
                ($"{ProjectDirectory}/FeatureCard.viu", FeatureCard),
                ($"{ProjectDirectory}/OverviewView.viu",
                    "<template>\n" +
                    "    " + usage + "\n" +
                    "</template>\n"),
            ],
            RootNamespace,
            ProjectDirectory);
        return outcome.Diagnostics;
    }

    // A minimal stand-in for the shipping Assimalign.Viu.Components surface plus a packaged component
    // that uses it, compiled to metadata. Keeping it synthetic keeps this generator test project free of
    // a runtime-library reference while exercising the exact symbol shapes the catalog reader looks for:
    // the assembly name it filters on, the adopted component/base contracts, and the attribute.
    private static IReadOnlyList<MetadataReference> CompilePackagedComponent()
    {
        var components = Emit(
            "Assimalign.Viu.Components",
            """
            namespace Assimalign.Viu.Components
            {
                public sealed class ComponentContext { }

                public sealed class ComponentRenderFrame { }

                public delegate object ComponentRenderer(ComponentRenderFrame frame);

                public interface IComponent
                {
                    ComponentRenderer Setup(ComponentContext context);
                }

                public abstract class ComponentBase
                {
                    protected ComponentContext Context { get; set; }
                }

                [System.AttributeUsage(System.AttributeTargets.Property)]
                public sealed class ParameterAttribute : System.Attribute
                {
                    public ParameterAttribute() { }
                    public ParameterAttribute(string name) { Name = name; }
                    public string Name { get; set; }
                    public bool IsRequired { get; set; }
                }
            }
            """,
            []);

        var package = Emit(
            "Widgets",
            """
            namespace Widgets
            {
                public sealed class PackagedCard :
                    Assimalign.Viu.Components.ComponentBase,
                    Assimalign.Viu.Components.IComponent
                {
                    public Assimalign.Viu.Components.ComponentRenderer Setup(
                        Assimalign.Viu.Components.ComponentContext context)
                    {
                        Context = context;
                        return frame => null;
                    }

                    [Assimalign.Viu.Components.Parameter(IsRequired = true)]
                    public string Title { get; set; }

                    [Assimalign.Viu.Components.Parameter]
                    public int Rating { get; set; }
                }

                public sealed class EmptyPackagedCard :
                    Assimalign.Viu.Components.ComponentBase,
                    Assimalign.Viu.Components.IComponent
                {
                    public Assimalign.Viu.Components.ComponentRenderer Setup(
                        Assimalign.Viu.Components.ComponentContext context)
                    {
                        Context = context;
                        return frame => null;
                    }
                }

                public sealed class PackagedImperative :
                    Assimalign.Viu.Components.ComponentBase,
                    Assimalign.Viu.Components.IComponent
                {
                    public object Parameters { get; } = new object();

                    public Assimalign.Viu.Components.ComponentRenderer Setup(
                        Assimalign.Viu.Components.ComponentContext context)
                    {
                        Context = context;
                        return frame => null;
                    }
                }
            }
            """,
            [components]);

        return [components, package];
    }

    private static MetadataReference Emit(
        string assemblyName,
        string source,
        IReadOnlyList<MetadataReference> references)
    {
        var allReferences = new List<MetadataReference>(references.Count + 1)
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
        allReferences.AddRange(references);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            allReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        if (!emit.Success)
        {
            throw new InvalidOperationException(
                $"The synthetic '{assemblyName}' assembly failed to compile: "
                + string.Join(
                    Environment.NewLine,
                    emit.Diagnostics.Where(d => d.Severity == RoslynDiagnosticSeverity.Error)));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
