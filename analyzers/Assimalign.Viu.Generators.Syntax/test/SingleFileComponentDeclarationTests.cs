using System;
using System.Linq;

using Shouldly;
using Xunit;

// Both the Viu syntax cluster and Roslyn expose DiagnosticSeverity; alias the Roslyn one the generator
// reports through.
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Tests for the attribute-declared component surface ([CMP-26]–[CMP-31]): <c>[Parameter]</c> properties
/// and <c>[Event]</c> partial methods in an <c>@script</c> block become the generated partial's
/// static <c>ComponentContract</c>, its per-render argument binding, and its typed emit
/// implementations. The declaration is read and emitted entirely at build time — the sanctioned
/// metaprogramming path — so nothing about it is discovered by reflection at runtime.
/// </summary>
public sealed class SingleFileComponentDeclarationTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void ParameterAttribute_SynthesizesDeclarations_WithCamelCaseDerivedNames()
    {
        // [CMP-27] The canonical argument name is the camel-case spelling of the property name, which is
        // exactly the name an imperative `new ComponentParameter("title")` declaration would have used.
        // The declaration is embedded in the immutable generated component contract.
        var generated = Generate(
            "FeatureCard",
            "<template>\n" +
            "    <h2>{{ Title }}</h2>\n" +
            "</template>\n" +
            "\n" +
            "@script {\n" +
            "    [Parameter(IsRequired = true)]\n" +
            "    public string Title { get; set; } = \"Untitled\";\n" +
            "\n" +
            "    [Parameter]\n" +
            "    public string Eyebrow { get; set; } = \"Feature\";\n" +
            "}\n");

        generated.ShouldContain(
            "new global::Assimalign.Viu.Components.ComponentParameter(\"title\", isRequired: true, "
            + "parameterType: typeof(string)),");
        generated.ShouldContain(
            "new global::Assimalign.Viu.Components.ComponentParameter(\"eyebrow\", "
            + "parameterType: typeof(string)),");
        generated.ShouldContain("parameters: new global::Assimalign.Viu.Components.ComponentParameter[]");
    }

    [Theory]
    // [CMP-27] The derivation is the standard camel-case rule: a leading upper-case run lower-cases
    // whole, except that its last letter stays capitalized when it begins the next word.
    [InlineData("Title", "title")]
    [InlineData("ModelValue", "modelValue")]
    [InlineData("URL", "url")]
    [InlineData("HTMLContent", "htmlContent")]
    [InlineData("value", "value")]
    public void ParameterAttribute_DerivesTheCanonicalName_FromThePropertyName(
        string propertyName,
        string argumentName)
    {
        var generated = Generate(
            "Widget",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            $"    [Parameter] public string {propertyName} {{ get; set; }} = \"\";\n" +
            "}\n");

        generated.ShouldContain($"ComponentParameter(\"{argumentName}\", parameterType: typeof(string))");
    }

    [Fact]
    public void ParameterAttribute_ExplicitName_OverridesTheDerivedName()
    {
        // A namespaced template spelling such as `update:modelValue` cannot be derived from any C#
        // identifier, so the attribute takes an explicit constant name.
        var generated = Generate(
            "Widget",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [Parameter(\"model-value\")] public int Rating { get; set; }\n" +
            "}\n");

        generated.ShouldContain("ComponentParameter(\"model-value\", parameterType: typeof(int))");
        generated.ShouldContain(
            "RenderGlue.TryReadParameter<int>(bindings.Parameters, \"model-value\", " +
            "out int __ViuParameterValueRating)");
    }

    [Fact]
    public void ParameterBinding_CapturesTheAuthoredInitializer_AsThePerInstanceDefault()
    {
        // [CMP-29] The authored initializer IS the default: it is captured once per mounted instance
        // before the first assignment and restored on every render pass where the parent supplies no
        // argument, so a parent that stops passing an argument gets the declared default back rather than
        // the last supplied value.
        var generated = Generate(
            "Widget",
            "<template>\n" +
            "    <div>{{ Eyebrow }}</div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [Parameter] public string Eyebrow { get; set; } = \"Feature\";\n" +
            "}\n");

        generated.ShouldContain("private string __ViuParameterDefaultEyebrow = default!;");
        generated.ShouldContain("__ViuParameterDefaultEyebrow = Eyebrow;");
        generated.ShouldContain(
            "Eyebrow = global::Assimalign.Viu.Generated.RenderGlue.TryReadParameter<string>(" +
            "bindings.Parameters, \"eyebrow\", out string __ViuParameterValueEyebrow)");
        generated.ShouldContain("? __ViuParameterValueEyebrow");
        generated.ShouldContain(": __ViuParameterDefaultEyebrow;");

        // Bound once during setup so OnSetup and the lifecycle see the initial arguments, and again at
        // the head of every render pass, after Core has replaced the argument snapshot.
        generated.ShouldContain("Context = context;\n            __ViuBindParameters(context.Bindings);\n            OnSetup();");
        generated.ShouldContain("return frame =>\n            {\n                __ViuBindParameters(context.Bindings);");
    }

    [Fact]
    public void RequiredModifier_DeclaresRequiredness_AndKeepsTheParameterlessActivatorCompiling()
    {
        // [CMP-28] The C# `required` modifier declares requiredness to Viu. Viu's activator is a
        // parameterless `new T()` with no object initializer, so the scaffold takes responsibility for the
        // required members — otherwise a `required` [Parameter] would make every registration fail to
        // compile.
        var generated = Generate(
            "Widget",
            "<template>\n" +
            "    <div>{{ Title }}</div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [Parameter] public required string Title { get; set; }\n" +
            "}\n");

        generated.ShouldContain("ComponentParameter(\"title\", isRequired: true, parameterType: typeof(string))");
        generated.ShouldContain("[global::System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        generated.ShouldContain("public Widget()");
    }

    [Fact]
    public void EventAttribute_SynthesizesTheDeclaration_AndImplementsTheTypedEmit()
    {
        // [CMP-30] The event name is spelled once, in the attribute. The generated implementation of the
        // authored `partial void` turns a strongly typed call into the untyped Emit the runtime dispatches,
        // and the synthesized validator asserts the emitted argument count.
        var generated = Generate(
            "RatingControl",
            "<template>\n" +
            "    <button @click=\"Changed(1)\">go</button>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [Event] public partial void Changed(int rating);\n" +
            "\n" +
            "    [Event(\"update:modelValue\")] public partial void ModelUpdated(int rating);\n" +
            "\n" +
            "    [Event] public partial void Dismissed();\n" +
            "}\n");

        generated.ShouldContain(
            "new global::Assimalign.Viu.Components.ComponentEvent(\"changed\", static arguments => arguments.Count == 1),");
        generated.ShouldContain(
            "new global::Assimalign.Viu.Components.ComponentEvent(\"update:modelValue\", static arguments => arguments.Count == 1),");
        generated.ShouldContain(
            "new global::Assimalign.Viu.Components.ComponentEvent(\"dismissed\", static arguments => arguments.Count == 0),");
        generated.ShouldContain("public partial void Changed(int rating)\n            => Context!.Emit(\"changed\", new object?[] { rating });");
        generated.ShouldContain("public partial void Dismissed()\n            => Context!.Emit(\"dismissed\", global::System.Array.Empty<object?>());");
        generated.ShouldContain("events: new global::Assimalign.Viu.Components.ComponentEvent[]");
    }

    [Fact]
    public void ImperativeDeclarations_StayUntouched_WhenNoAttributeIsUsed()
    {
        // The compatibility bar: a component that declares its surface imperatively — the form every
        // existing component uses — gets no synthesized declaration, no binding method, and no explicit
        // interface implementation. Its own `Parameters` property remains the implicit implementation
        // [CMP-31].
        var generated = Generate(
            "Legacy",
            "<template>\n" +
            "    <div>{{ Title }}</div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    using System.Collections.Generic;\n" +
            "    using Assimalign.Viu.Components;\n" +
            "\n" +
            "    public IReadOnlyList<IComponentParameter> Parameters { get; } =\n" +
            "    [\n" +
            "        new ComponentParameter(\"title\", isRequired: true),\n" +
            "    ];\n" +
            "\n" +
            "    public string Title => Context.Arguments.Get<string>(\"title\") ?? \"Untitled\";\n" +
            "}\n");

        generated.ShouldNotContain("__ViuDeclaredParameters");
        generated.ShouldNotContain("__ViuBindParameters");
        generated.ShouldNotContain("IComponentTemplate.Parameters");
    }

    [Fact]
    public void MixingAttributeAndImperativeParameters_IsAConflictError()
    {
        // [CMP-31] The coexistence rule. Both forms would compile — the generated explicit interface
        // implementation silently wins over the authored implicit one — so the mix is rejected outright
        // rather than resolved by a precedence rule nobody can see. It also keeps the attribute surface
        // COMPLETE, which is what lets a consumer's template be checked against it.
        var diagnostics = Run(
            "Mixed",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    using System.Collections.Generic;\n" +
            "    using Assimalign.Viu.Components;\n" +
            "\n" +
            "    public IReadOnlyList<IComponentParameter> Parameters { get; } = [];\n" +
            "\n" +
            "    [Parameter] public string Title { get; set; } = \"\";\n" +
            "}\n");

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1207");
        diagnostic.Severity.ShouldBe(RoslynDiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("[Parameter]");
    }

    [Fact]
    public void MixingAttributeAndImperativeEvents_IsAConflictError()
    {
        var diagnostics = Run(
            "Mixed",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    using System.Collections.Generic;\n" +
            "    using Assimalign.Viu.Components;\n" +
            "\n" +
            "    public IReadOnlyList<IComponentEvent> Events { get; } = [];\n" +
            "\n" +
            "    [Event] public partial void Changed(int rating);\n" +
            "}\n");

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1207");
        diagnostic.GetMessage().ShouldContain("[Event]");
    }

    [Fact]
    public void ImperativeParameters_AndAttributeEvents_Coexist()
    {
        // The rule is per kind, not per component: parameters and events are independent surfaces, so a
        // component may keep its imperative parameter collection while declaring its events by attribute.
        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Split.viu",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    using System.Collections.Generic;\n" +
            "    using Assimalign.Viu.Components;\n" +
            "\n" +
            "    public IReadOnlyList<IComponentParameter> Parameters { get; } = [];\n" +
            "\n" +
            "    [Event] public partial void Changed(int rating);\n" +
            "}\n",
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(outcome, "Split.SingleFileComponent.g.cs");
        generated.ShouldContain("events: new global::Assimalign.Viu.Components.ComponentEvent[]");
        generated.ShouldNotContain("__ViuDeclaredParameters");
    }

    [Fact]
    public void DuplicateDerivedNames_AreReportedOnTheSecondDeclaration()
    {
        // Two declarations of one name would make Core's parameter alias table throw at mount, so the
        // duplicate is a build error instead.
        var diagnostics = Run(
            "Widget",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [Parameter] public string Title { get; set; } = \"\";\n" +
            "    [Parameter(\"title\")] public string Heading { get; set; } = \"\";\n" +
            "}\n");

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1208");
        diagnostic.GetMessage().ShouldContain("title");
    }

    [Theory]
    // A get-only property has nowhere to receive the argument; a static property is not per-instance
    // state; a non-partial or bodied [Event] method cannot be implemented by the scaffold; and a
    // non-constant attribute argument cannot be read at build time.
    [InlineData("    [Parameter] public string Title => \"x\";\n")]
    [InlineData("    [Parameter] public static string Title { get; set; } = \"\";\n")]
    [InlineData("    [Parameter(Name = Constant)] public string Title { get; set; } = \"\";\n    private const string Constant = \"title\";\n")]
    [InlineData("    [Event] public void Changed(int rating) { }\n")]
    [InlineData("    [Event] public partial int Changed();\n")]
    [InlineData("    [Event] public partial void Changed(ref int rating);\n")]
    public void UnsupportedDeclarationShapes_AreReported(string member)
    {
        var diagnostics = Run(
            "Widget",
            "<template>\n" +
            "    <div></div>\n" +
            "</template>\n" +
            "@script {\n" +
            member +
            "}\n");

        diagnostics
            .Where(diagnostic => diagnostic.Id == "VIU1209")
            .ShouldNotBeEmpty();
    }

    [Fact]
    public void DeclarationDiagnostic_ResolvesToTheViuCoordinate()
    {
        // Every projection-owned script rule maps onto the .viu file through the same block-to-file
        // composition the parse diagnostics use, so the declaration errors land on the authored member.
        var diagnostics = Run(
            "Widget",
            "<template>\n" +          // line 1
            "    <div></div>\n" +     // line 2
            "</template>\n" +         // line 3
            "@script {\n" +           // line 4
            "    [Parameter] public string Title => \"x\";\n" +  // line 5
            "}\n");                   // line 6

        var span = diagnostics.ShouldHaveSingleItem().Location.GetLineSpan();
        span.Path.ShouldBe($"{ProjectDirectory}/Widget.viu");
        span.StartLinePosition.Line.ShouldBe(4); // .viu file line 5, zero-based
    }

    [Fact]
    public void StyleOnlyComponent_IgnoresDeclarations()
    {
        // A component without a template block is not an IComponentTemplate at all, so it has no
        // Parameters/Events surface to hang a declaration on and the reader never runs.
        var generated = Generate(
            "Styles",
            "@script {\n" +
            "    [Parameter] public string Title { get; set; } = \"\";\n" +
            "}\n" +
            "<style>\n" +
            "    div { color: red; }\n" +
            "</style>\n");

        generated.ShouldNotContain("__ViuDeclaredParameters");
    }

    private static string Generate(string componentName, string source)
    {
        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/{componentName}.viu",
            source,
            RootNamespace,
            ProjectDirectory);
        outcome.Diagnostics
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty();
        return GeneratorTestHarness.GeneratedSource(
            outcome,
            $"{componentName}.SingleFileComponent.g.cs");
    }

    private static System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Run(
        string componentName,
        string source)
        => GeneratorTestHarness.Run(
            $"{ProjectDirectory}/{componentName}.viu",
            source,
            RootNamespace,
            ProjectDirectory).Diagnostics;
}
