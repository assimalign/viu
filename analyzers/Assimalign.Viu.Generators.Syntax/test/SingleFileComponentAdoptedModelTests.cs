using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins the assembly and per-component source shapes adopted by [SFC-CG-1] through [SFC-CG-4].
/// </summary>
public sealed class SingleFileComponentAdoptedModelTests
{
    private const string ProjectDirectory = "C:/project";
    private const string RootNamespace = "Demo";

    [Fact]
    public void Scaffold_EmitsContractRegistrationAndFrameTakingSetup()
    {
        const string source =
            "<template>\n" +
            "    <button @click=\"Changed(Title)\">{{ Title }}</button>\n" +
            "</template>\n" +
            "@script {\n" +
            "    [global::Assimalign.Viu.Components.Parameter(IsRequired = true)]\n" +
            "    public string Title { get; set; } = \"default\";\n" +
            "\n" +
            "    [global::Assimalign.Viu.Components.Event]\n" +
            "    public partial void Changed(string value);\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            ProjectDirectory + "/ActionButton.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "ActionButton.SingleFileComponent.g.cs");

        generated.ShouldContain(
            "partial class ActionButton : global::Assimalign.Viu.Components.ComponentBase, " +
            "global::Assimalign.Viu.Components.IComponent");
        generated.ShouldContain(
            "global::Assimalign.Viu.Components.ComponentContract __ViuContract = new(");
        generated.ShouldContain(
            "ComponentParameter(\"title\", isRequired: true, parameterType: typeof(string))");
        generated.ShouldContain("ComponentEvent(\"changed\"");
        generated.ShouldContain("private static int __ViuGetRenderCacheSize()");
        generated.ShouldContain("renderCacheSizeProvider: __ViuGetRenderCacheSize");
        generated.ShouldContain(
            "ComponentReference.ForName(\"ActionButton\")");
        generated.ShouldContain(
            "ComponentRenderer global::Assimalign.Viu.Components.IComponent.Setup(");
        generated.ShouldContain("ComponentContext context)");
        generated.ShouldContain("Context = context;");
        generated.ShouldContain("__ViuBindParameters(context.Bindings);");
        generated.ShouldContain("OnSetup();");
        generated.ShouldContain("return __ViuRender(this, frame);");
    }

    [Fact]
    public void AssemblyEmission_PublishesOneCatalogAndOneGeneratedGlueType()
    {
        var outcome = GeneratorTestHarness.RunAll(
            new[]
            {
                (ProjectDirectory + "/Zeta.viu", "<template><div>Zeta</div></template>"),
                (ProjectDirectory + "/Alpha.viu", "<template><div>Alpha</div></template>"),
            },
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var assemblySources = outcome.Sources
            .Where(source => string.Equals(
                source.HintName,
                SingleFileComponentAssemblyEmitter.HintName,
                StringComparison.Ordinal))
            .ToArray();
        var assemblySource = assemblySources.ShouldHaveSingleItem().SourceText.ToString()
            .Replace("\r\n", "\n");

        assemblySource.ShouldContain("namespace Demo");
        assemblySource.ShouldContain("public static class GeneratedViuComponents");
        assemblySource.ShouldContain(
            "public static void Register(global::Assimalign.Viu.Components.ComponentFactory factory)");
        assemblySource.IndexOf("global::Demo.Alpha.__ViuRegistration", StringComparison.Ordinal)
            .ShouldBeLessThan(
                assemblySource.IndexOf("global::Demo.Zeta.__ViuRegistration", StringComparison.Ordinal));
        assemblySource.ShouldContain(
            "[global::System.CodeDom.Compiler.GeneratedCode(" +
            "\"Assimalign.Viu.Generators.Syntax\", \"1.0.0.0\")]");
        assemblySource.Split(new[] { "internal static class RenderGlue" }, StringSplitOptions.None)
            .Length.ShouldBe(2);
        assemblySource.ShouldContain(
            "TValue Unwrap<TValue>(\n" +
            "            global::Assimalign.Viu.Reactivity.IReactiveReference<TValue> reference)");
    }

    [Fact]
    public void StyleEmission_RetainsBundlingAndModulesWithoutDeferredRuntimeSurface()
    {
        const string source =
            "<template>\n" +
            "    <div :class=\"Style.box\" />\n" +
            "</template>\n" +
            "<style scoped module=\"Style\">\n" +
            "    .box { color: v-bind(Color); }\n" +
            "</style>\n" +
            "@script {\n" +
            "    public string Color { get; set; } = \"red\";\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            ProjectDirectory + "/Styled.viu",
            source,
            RootNamespace,
            ProjectDirectory);
        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Styled.SingleFileComponent.g.cs");

        generated.ShouldContain("internal const string ExtractedStyles");
        generated.ShouldContain("internal static class Style");
        generated.ShouldContain("public const string box");
        generated.ShouldNotContain("Scope" + "Identifier");
        generated.ShouldNotContain("internal const string ScopeId");
        generated.ShouldNotContain("ApplyCssVariables");
    }

    [Fact]
    public void Scaffold_UsesQualifiedFrameApisWithoutLegacyHelperAbi()
    {
        var outcome = GeneratorTestHarness.Run(
            ProjectDirectory + "/Panel.viu",
            "<template><section>{{ Message }}</section></template>",
            RootNamespace,
            ProjectDirectory);
        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Panel.SingleFileComponent.g.cs");

        generated.ShouldContain("ComponentRenderFrame frame");
        generated.ShouldNotContain("using static");
        generated.ShouldNotContain("Render" + "Helpers");
        generated.ShouldNotContain("Block" + "Token");
        generated.ShouldNotContain("IComponent" + "Template");
        generated.ShouldNotContain("IComponent" + "Context");
        generated.ShouldNotContain("IComponentHotReload" + "Metadata");
        generated.ShouldNotContain("Scope" + "Identifier");
    }
}
