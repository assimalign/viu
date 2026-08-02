using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins [V01.01.06.05]'s generator-owned hot-reload metadata contract for canonical <c>.viu</c> and
/// compatible <c>.vue</c> sources.
/// </summary>
public sealed class SingleFileComponentHotReloadMetadataTests
{
    private const string HandlerHintName =
        "Viu.SingleFileComponentHotReloadMetadataUpdateHandler.g.cs";
    private const string RootNamespace = "Demo";

    /// <summary>
    /// Debug metadata uses a project-relative, content-independent identifier, while edits invalidate
    /// only the matching template, script, or style category hash. This is the generator-side input to
    /// Vue-parity re-render, reload, and stylesheet-swap decisions:
    /// https://github.com/vuejs/core/blob/v3.5.34/packages/runtime-core/src/hmr.ts.
    /// </summary>
    /// <param name="extension">The canonical or compatibility source extension.</param>
    [Theory]
    [InlineData(".viu")]
    [InlineData(".vue")]
    public void DebugMetadata_PathIdentifierIsStable_AndOnlyEditedBlockHashChanges(string extension)
    {
        const string firstProjectDirectory = "C:/checkout/First";
        const string secondProjectDirectory = "D:/checkout/Second";
        var relativePath = "Components/Counter" + extension;

        var baseline = Generate(
            firstProjectDirectory + "/" + relativePath,
            firstProjectDirectory,
            extension,
            templateValue: "first template",
            scriptValue: "first script",
            styleValue: "red");
        var relocated = Generate(
            secondProjectDirectory + "/" + relativePath,
            secondProjectDirectory,
            extension,
            templateValue: "first template",
            scriptValue: "first script",
            styleValue: "red");
        var templateEdited = Generate(
            firstProjectDirectory + "/" + relativePath,
            firstProjectDirectory,
            extension,
            templateValue: "edited template",
            scriptValue: "first script",
            styleValue: "red");
        var scriptEdited = Generate(
            firstProjectDirectory + "/" + relativePath,
            firstProjectDirectory,
            extension,
            templateValue: "first template",
            scriptValue: "edited script",
            styleValue: "red");
        var styleEdited = Generate(
            firstProjectDirectory + "/" + relativePath,
            firstProjectDirectory,
            extension,
            templateValue: "first template",
            scriptValue: "first script",
            styleValue: "blue");

        baseline.GeneratedSource.ShouldContain(
            "global::Assimalign.Viu.Components.IComponentHotReloadMetadata");
        baseline.GeneratedSource.ShouldContain(
            "IComponentHotReloadMetadata.ComponentIdentifier");
        baseline.GeneratedSource.ShouldContain(
            "IComponentHotReloadMetadata.TemplateUpdateMarkerType");
        baseline.GeneratedSource.ShouldContain(
            "IComponentHotReloadMetadata.ScriptUpdateMarkerType");
        baseline.GeneratedSource.ShouldNotContain(
            "ComponentHotReloadBridge");
        baseline.GeneratedSource.ShouldContain(
            "private static class SingleFileComponentTemplateUpdateMarker");
        baseline.GeneratedSource.ShouldContain(
            "private static class SingleFileComponentScriptUpdateMarker");
        baseline.GeneratedSource.ShouldContain(
            "private static class SingleFileComponentStyleUpdateMarker");
        baseline.GeneratedSource.ShouldContain(
            "IComponentHotReloadMetadata.StyleUpdateMarkerType");
        baseline.GeneratedSource.ShouldNotContain(
            "_hotReloadRenderCache = new object?[RenderCacheSize]");
        baseline.GeneratedSource.ShouldNotContain(
            "readonly object?[] _hotReloadRenderCache");
        baseline.GeneratedSource.ShouldContain(
            "NormalizeRoot(Render(this, _hotReloadRenderCache))");
        baseline.GeneratedSource.ShouldContain(
            "global::System.GC.KeepAlive(\"" + baseline.TemplateContentHash + "\");");
        baseline.GeneratedSource.ShouldContain(
            "internal static int RenderCacheSize => ");
        baseline.GeneratedSource.ShouldContain(
            "internal static string HotReloadTemplateContentHash => ");
        baseline.GeneratedSource.ShouldContain(
            "internal static string HotReloadScriptContentHash => ");
        baseline.GeneratedSource.ShouldContain(
            "internal static string HotReloadStyleContentHash => ");
        baseline.GeneratedSource.ShouldContain(
            "internal static string ExtractedStyles => ");
        baseline.GeneratedSource.ShouldNotContain(
            "internal const int RenderCacheSize");
        baseline.GeneratedSource.ShouldNotContain(
            "internal const string HotReload");
        baseline.GeneratedSource.ShouldNotContain(
            "internal const string ExtractedStyles");
        baseline.GeneratedSource.ShouldContain(
            "global::System.GC.KeepAlive(\"" + baseline.ScriptContentHash + "\");");
        baseline.GeneratedSource.ShouldContain(
            "global::System.GC.KeepAlive(\"" + baseline.StyleContentHash + "\");");
        baseline.ComponentIdentifier.ShouldStartWith("viu-");
        baseline.ComponentIdentifier.Length.ShouldBe(20);
        baseline.TemplateContentHash.Length.ShouldBe(16);
        baseline.ScriptContentHash.Length.ShouldBe(16);
        baseline.StyleContentHash.Length.ShouldBe(16);

        relocated.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        relocated.TemplateContentHash.ShouldBe(baseline.TemplateContentHash);
        relocated.ScriptContentHash.ShouldBe(baseline.ScriptContentHash);
        relocated.StyleContentHash.ShouldBe(baseline.StyleContentHash);

        templateEdited.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        templateEdited.TemplateContentHash.ShouldNotBe(baseline.TemplateContentHash);
        templateEdited.ScriptContentHash.ShouldBe(baseline.ScriptContentHash);
        templateEdited.StyleContentHash.ShouldBe(baseline.StyleContentHash);
        templateEdited.GeneratedSource.ShouldContain(
            "global::System.GC.KeepAlive(\"" + templateEdited.TemplateContentHash + "\");");

        scriptEdited.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        scriptEdited.TemplateContentHash.ShouldBe(baseline.TemplateContentHash);
        scriptEdited.ScriptContentHash.ShouldNotBe(baseline.ScriptContentHash);
        scriptEdited.StyleContentHash.ShouldBe(baseline.StyleContentHash);

        styleEdited.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        styleEdited.TemplateContentHash.ShouldBe(baseline.TemplateContentHash);
        styleEdited.ScriptContentHash.ShouldBe(baseline.ScriptContentHash);
        styleEdited.StyleContentHash.ShouldNotBe(baseline.StyleContentHash);
    }

    /// <summary>
    /// One Roslyn incremental driver retains the path identity and independently updates only the
    /// edited block hash as its AdditionalText is replaced.
    /// </summary>
    /// <param name="extension">The canonical or compatibility source extension.</param>
    [Theory]
    [InlineData(".viu")]
    [InlineData(".vue")]
    public void IncrementalEdits_ChangeOnlyTheCorrespondingBlockHash(string extension)
    {
        const string projectDirectory = "C:/project";
        var path = projectDirectory + "/Components/Counter" + extension;
        var compilation = GeneratorTestHarness.CreateCompilation();
        AdditionalText currentFile = new InMemoryAdditionalText(
            path,
            Source(extension, "first template", "first script", "red"));
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create(currentFile),
            RootNamespace,
            projectDirectory,
            configuration: "Debug");

        driver = driver.RunGenerators(compilation);
        var baseline = GeneratedMetadata(driver);
        var baselineHandler = GeneratedHandler(driver);

        var templateFile = new InMemoryAdditionalText(
            path,
            Source(extension, "edited template", "first script", "red"));
        driver = driver.ReplaceAdditionalText(currentFile, templateFile).RunGenerators(compilation);
        currentFile = templateFile;
        var templateEdited = GeneratedMetadata(driver);
        var templateEditedHandler = GeneratedHandler(driver);

        var scriptFile = new InMemoryAdditionalText(
            path,
            Source(extension, "first template", "edited script", "red"));
        driver = driver.ReplaceAdditionalText(currentFile, scriptFile).RunGenerators(compilation);
        currentFile = scriptFile;
        var scriptEdited = GeneratedMetadata(driver);
        var scriptEditedHandler = GeneratedHandler(driver);

        var styleFile = new InMemoryAdditionalText(
            path,
            Source(extension, "first template", "first script", "blue"));
        driver = driver.ReplaceAdditionalText(currentFile, styleFile).RunGenerators(compilation);
        currentFile = styleFile;
        var styleEdited = GeneratedMetadata(driver);
        var styleEditedHandler = GeneratedHandler(driver);

        var restoredFile = new InMemoryAdditionalText(
            path,
            Source(extension, "first template", "first script", "red"));
        driver = driver.ReplaceAdditionalText(currentFile, restoredFile).RunGenerators(compilation);
        var restored = GeneratedMetadata(driver);
        var restoredHandler = GeneratedHandler(driver);

        templateEdited.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        templateEdited.TemplateContentHash.ShouldNotBe(baseline.TemplateContentHash);
        templateEdited.ScriptContentHash.ShouldBe(baseline.ScriptContentHash);
        templateEdited.StyleContentHash.ShouldBe(baseline.StyleContentHash);

        scriptEdited.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        scriptEdited.TemplateContentHash.ShouldBe(baseline.TemplateContentHash);
        scriptEdited.ScriptContentHash.ShouldNotBe(baseline.ScriptContentHash);
        scriptEdited.StyleContentHash.ShouldBe(baseline.StyleContentHash);

        styleEdited.ComponentIdentifier.ShouldBe(baseline.ComponentIdentifier);
        styleEdited.TemplateContentHash.ShouldBe(baseline.TemplateContentHash);
        styleEdited.ScriptContentHash.ShouldBe(baseline.ScriptContentHash);
        styleEdited.StyleContentHash.ShouldNotBe(baseline.StyleContentHash);

        templateEditedHandler.ShouldBe(baselineHandler);
        scriptEditedHandler.ShouldBe(baselineHandler);
        styleEditedHandler.ShouldBe(baselineHandler);
        restored.ShouldBe(baseline);
        restoredHandler.ShouldBe(baselineHandler);
    }

    /// <summary>Neither an unspecified configuration nor Release emits the development contract.</summary>
    /// <param name="extension">The source format.</param>
    /// <param name="configuration">The absent or Release configuration.</param>
    [Theory]
    [InlineData(".viu", null)]
    [InlineData(".viu", "Release")]
    [InlineData(".vue", null)]
    [InlineData(".vue", "Release")]
    public void DefaultAndRelease_MetadataIsAbsent(string extension, string? configuration)
    {
        const string projectDirectory = "C:/project";
        var outcome = GeneratorTestHarness.Run(
            projectDirectory + "/Counter" + extension,
            Source(extension, "template", "script", "red"),
            RootNamespace,
            projectDirectory,
            configuration);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Counter.SingleFileComponent.g.cs");
        generated.ShouldNotContain("IComponentHotReloadMetadata");
        generated.ShouldNotContain("HotReloadComponentIdentifier");
        generated.ShouldNotContain("HotReloadTemplateContentHash");
        generated.ShouldNotContain("HotReloadScriptContentHash");
        generated.ShouldNotContain("HotReloadStyleContentHash");
        outcome.Sources.ShouldNotContain(source =>
            string.Equals(source.HintName, HandlerHintName, StringComparison.Ordinal));
    }

    /// <summary>The explicit compiler-visible property opts a Release build into the same contract.</summary>
    /// <param name="extension">The source format.</param>
    [Theory]
    [InlineData(".viu")]
    [InlineData(".vue")]
    public void Release_ExplicitOptIn_EmitsMetadata(string extension)
    {
        const string projectDirectory = "C:/project";
        var outcome = GeneratorTestHarness.Run(
            projectDirectory + "/Counter" + extension,
            Source(extension, "template", "script", "red"),
            RootNamespace,
            projectDirectory,
            configuration: "Release",
            emitHotReloadMetadata: "true");

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Counter.SingleFileComponent.g.cs");
        generated.ShouldContain("IComponentHotReloadMetadata");
        generated.ShouldContain("internal static string HotReloadComponentIdentifier => ");
        outcome.Sources.ShouldContain(source =>
            string.Equals(source.HintName, HandlerHintName, StringComparison.Ordinal));
    }

    /// <summary>An explicit false value is an opt-out even when the project configuration is Debug.</summary>
    [Fact]
    public void Debug_ExplicitOptOut_OmitsMetadata()
    {
        const string projectDirectory = "C:/project";
        var outcome = GeneratorTestHarness.Run(
            projectDirectory + "/Counter.viu",
            Source(".viu", "template", "script", "red"),
            RootNamespace,
            projectDirectory,
            configuration: "Debug",
            emitHotReloadMetadata: "false");

        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Counter.SingleFileComponent.g.cs");
        generated.ShouldNotContain("IComponentHotReloadMetadata");
        generated.ShouldNotContain("HotReloadComponentIdentifier");
        outcome.Sources.ShouldNotContain(source =>
            string.Equals(source.HintName, HandlerHintName, StringComparison.Ordinal));
    }

    /// <summary>
    /// The enabled generator registers one exact consumer-assembly metadata-update handler, including
    /// when the current project has no component files yet.
    /// </summary>
    /// <param name="configuration">The project configuration.</param>
    /// <param name="emitHotReloadMetadata">The explicit override, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("Debug", null)]
    [InlineData("Release", "true")]
    public void DebugOrExplicitOptIn_EmitsExactAssemblyMetadataUpdateHandler(
        string configuration,
        string? emitHotReloadMetadata)
    {
        const string expected =
            """
            // <auto-generated/>
            #nullable enable

            [assembly: global::System.Reflection.Metadata.MetadataUpdateHandlerAttribute(
                typeof(global::Assimalign.Viu.Generated.SingleFileComponentHotReloadMetadataUpdateHandler))]

            namespace Assimalign.Viu.Generated
            {
                /// <summary>
                /// Forwards consumer-assembly metadata updates to Viu's component hot-reload runtime.
                /// </summary>
                internal static class SingleFileComponentHotReloadMetadataUpdateHandler
                {
                    /// <summary>
                    /// Deliberately preserves the previously recorded hashes for UpdateApplication to compare.
                    /// </summary>
                    /// <param name="updatedTypes">The types that may receive metadata updates.</param>
                    public static void ClearCache(global::System.Type[]? updatedTypes)
                    {
                    }

                    /// <summary>Applies updated component metadata after the runtime installs the delta.</summary>
                    /// <param name="updatedTypes">
                    /// The runtime's updated-type hint. Generated source edits can identify synthesized
                    /// implementation types instead of the mounted component, so Viu verifies every
                    /// registered component's content hashes.
                    /// </param>
                    public static void UpdateApplication(global::System.Type[]? updatedTypes)
                    {
                        global::Assimalign.Viu.ComponentHotReload.ApplyUpdates(updatedTypes);
                    }
                }
            }
            """;

        var driver = GeneratorTestHarness.CreateDriver(
                ImmutableArray<AdditionalText>.Empty,
                RootNamespace,
                "C:/project",
                configuration,
                emitHotReloadMetadata)
            .RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Diagnostics.ShouldBeEmpty();
        var handler = result.GeneratedSources.Single(source =>
            string.Equals(source.HintName, HandlerHintName, StringComparison.Ordinal));
        Normalize(handler.SourceText.ToString()).ShouldBe(Normalize(expected));
    }

    private static HotReloadMetadataValues Generate(
        string path,
        string projectDirectory,
        string extension,
        string templateValue,
        string scriptValue,
        string styleValue)
    {
        var outcome = GeneratorTestHarness.Run(
            path,
            Source(extension, templateValue, scriptValue, styleValue),
            RootNamespace,
            projectDirectory,
            configuration: "Debug");
        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Components.Counter.SingleFileComponent.g.cs");
        return new HotReloadMetadataValues(
            generated,
            GetterValue(generated, "HotReloadComponentIdentifier"),
            GetterValue(generated, "HotReloadTemplateContentHash"),
            GetterValue(generated, "HotReloadScriptContentHash"),
            GetterValue(generated, "HotReloadStyleContentHash"));
    }

    private static HotReloadMetadataValues GeneratedMetadata(GeneratorDriver driver)
    {
        var result = driver.GetRunResult().Results[0];
        var outcome = new GeneratorOutcome(result.GeneratedSources, result.Diagnostics);
        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Components.Counter.SingleFileComponent.g.cs");
        return new HotReloadMetadataValues(
            generated,
            GetterValue(generated, "HotReloadComponentIdentifier"),
            GetterValue(generated, "HotReloadTemplateContentHash"),
            GetterValue(generated, "HotReloadScriptContentHash"),
            GetterValue(generated, "HotReloadStyleContentHash"));
    }

    private static string GeneratedHandler(GeneratorDriver driver)
    {
        var result = driver.GetRunResult().Results[0];
        return result.GeneratedSources
            .Single(generated => string.Equals(
                generated.HintName,
                HandlerHintName,
                StringComparison.Ordinal))
            .SourceText
            .ToString();
    }

    private static string Source(
        string extension,
        string templateValue,
        string scriptValue,
        string styleValue)
    {
        if (string.Equals(extension, ".vue", StringComparison.Ordinal))
        {
            return
                "<template><div>" + templateValue + "</div></template>\n" +
                "<script setup lang=\"csharp\">public string SetupValue = \"setup\";</script>\n" +
                "<script lang=\"csharp\">public string Value = \"" + scriptValue + "\";</script>\n" +
                "<style scoped>.box { color: " + styleValue + "; }</style>\n";
        }

        return
            "<template>\n" +
            "    <div>" + templateValue + "</div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    public string Value = \"" + scriptValue + "\";\n" +
            "}\n" +
            "<style scoped>\n" +
            "    .box { color: " + styleValue + "; }\n" +
            "</style>\n";
    }

    private static string GetterValue(string generated, string name)
    {
        var marker = "internal static string " + name + " => \"";
        var start = generated.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, generated);
        start += marker.Length;
        var end = generated.IndexOf("\";", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, generated);
        return generated.Substring(start, end - start);
    }

    private static string Normalize(string value)
        => value.Replace("\r\n", "\n").TrimEnd() + "\n";

    private readonly record struct HotReloadMetadataValues(
        string GeneratedSource,
        string ComponentIdentifier,
        string TemplateContentHash,
        string ScriptContentHash,
        string StyleContentHash);
}
