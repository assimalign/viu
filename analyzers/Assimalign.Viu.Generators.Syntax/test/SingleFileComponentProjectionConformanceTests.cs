using System;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Tooling.Css;
using Assimalign.Viu.Tooling.SingleFileComponent;

using Shouldly;
using Xunit;

// The test namespace is nested under Assimalign.Viu.Syntax, so the base cluster's Diagnostic type is
// ambient and shadows Roslyn's; alias the Roslyn diagnostics the generator host reports.
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// The two-host conformance pin for the [V01.01.06.11] extraction (#258, acceptance criterion 1):
/// one projection, two hosts, identical output. Each fixture runs through BOTH surfaces — the
/// generator harness (path A: the MSBuild-shaped inputs <see cref="SingleFileComponentGenerator"/>
/// receives at build time) and the shared library facade driven directly (path B: the
/// <see cref="SingleFileComponentProjection.Project"/> call shape the language service uses in the
/// editor) — and asserts the full generated C# is ordinal-equal, the hint names match, and the
/// diagnostic sets match on id, severity, mapped <c>.viu</c> path/line/column, and message. One
/// projection serving both hosts is the guarantee: an editor squiggle can never drift from a build
/// error, because there is no second parse or second code path for it to drift through. This test
/// lives in the generator's suite because it is the only project that can legally see both hosts.
/// </summary>
public sealed class SingleFileComponentProjectionConformanceTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    // Canonical hybrid .viu exercising every emitter seam at once: a dynamic interpolation (render
    // body + #line (l,c)-(l,c) span map), a hoisted using + IReactiveReference member + method
    // (two-region @script #line map, unwrap seam), <style scoped> (ScopeId/ExtractedStyles),
    // <style module> (accessor class + $style resolution), and v-bind() (CSS-variable seam through
    // binding metadata).
    private const string CanonicalHybridSource =
        "<template>\n" +
        "    <div :class=\"$style.panel\">{{ Count }}</div>\n" +
        "</template>\n" +
        "\n" +
        "@script {\n" +
        "    using System;\n" +
        "    public Assimalign.Viu.Reactivity.IReactiveReference<int> Count = default!;\n" +
        "    public int Doubled() => Math.Max(Count.Value * 2, 0);\n" +
        "}\n" +
        "\n" +
        "<style scoped>\n" +
        "    .box { color: v-bind(Count); }\n" +
        "</style>\n" +
        "\n" +
        "<style module>\n" +
        "    .panel { padding: 1px; }\n" +
        "</style>\n";

    // Legacy @template container ([V01.01.06.10] transition window): both hosts must report the same
    // VIU1002 migration warning AND compile the same render function.
    private const string LegacyContainerSource =
        "@template {\n" +
        "    <div>legacy</div>\n" +
        "}\n";

    // Tag-based .vue compatibility ([V01.01.06.09]): C# script, scoped style, and a named CSS module.
    private const string VueTagSource =
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

    // .vue two-script merge: an ordinary script plus <script setup> with a hoisted using.
    private const string VueSetupSource =
        "<template><div>{{ Ordinary }} {{ SetupCount }}</div></template>\n" +
        "<script lang=\"csharp\">public int Ordinary = 1;</script>\n" +
        "<script setup lang=\"csharp\">using System;\n" +
        "public int SetupCount = Math.Max(2, 1);</script>";

    // Degenerate shapes guarding the conditional seams: style-only (no bridge, no using static),
    // script-only (no render function), and a fully static template (the RenderCacheSize slot path).
    private const string StyleOnlySource =
        "<style scoped>\n" +
        "    .box { color: red; }\n" +
        "</style>\n";

    private const string ScriptOnlySource =
        "@script {\n" +
        "    public int Value;\n" +
        "}\n";

    private const string StaticTemplateSource =
        "<template>\n" +
        "    <section><span>static</span></section>\n" +
        "</template>\n";

    /// <summary>The fixture rows: leaf file name, source text, and the build configuration.</summary>
    public static TheoryData<string, string, string?> Fixtures => new()
    {
        { "Counter.viu", CanonicalHybridSource, null },
        { "Legacy.viu", LegacyContainerSource, null },
        { "Card.vue", VueTagSource, null },
        { "Setup.vue", VueSetupSource, null },
        { "AppStyles.viu", StyleOnlySource, null },
        { "Bare.viu", ScriptOnlySource, null },
        { "Static.viu", StaticTemplateSource, null },
        // One Debug row so the hot-reload metadata emitter ([V01.01.06.05]) sits inside the
        // conformance boundary, not just the Release shape.
        { "Counter.viu", CanonicalHybridSource, "Debug" },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Project_DrivenThroughLibraryFacadeAndGeneratorHarness_ProducesIdenticalOutput(
        string fileName,
        string content,
        string? configuration)
    {
        var path = $"{ProjectDirectory}/{fileName}";

        // Path A: the generator host, fed exactly the inputs MSBuild would.
        var outcome = GeneratorTestHarness.Run(path, content, RootNamespace, ProjectDirectory, configuration);

        // Path B: the library facade driven directly with the same source read — the call shape the
        // language service runs in the editor.
        var input = CreateInput(path, content, configuration);
        var result = SingleFileComponentProjection.Project(input, CancellationToken.None);

        if (string.Equals(configuration, "Debug", StringComparison.Ordinal))
        {
            // Guard that the Debug row actually exercises the metadata emitter.
            result.Model.HotReloadMetadata.ShouldNotBeNull();
        }

        // Hint-name equality: the harness produced exactly one source under the model's hint name
        // (the Debug row also emits the per-assembly hot-reload handler, which is host plumbing
        // outside the per-component projection).
        var componentSource = outcome.Sources
            .Where(source => string.Equals(source.HintName, result.Model.HintName, StringComparison.Ordinal))
            .ShouldHaveSingleItem();

        // Full-source ordinal equality after the same LF normalization on both sides.
        var generatorText = componentSource.SourceText.ToString().Replace("\r\n", "\n");
        var libraryText = SingleFileComponentSourceEmitter.Emit(result.Model).Replace("\r\n", "\n");
        libraryText.ShouldBe(generatorText);

        // Diagnostic-set equality on id, severity, mapped .viu path/line/column, and message, so
        // editor squiggles cannot drift from build errors.
        var generatorDiagnostics = outcome.Diagnostics.Select(Describe).ToList();
        var libraryDiagnostics = result.Diagnostics.Select(Describe).ToList();
        libraryDiagnostics.ShouldBe(generatorDiagnostics);
    }

    // Mirrors the generator's ReadFile: the same name resolution, scope-id derivation, format
    // detection, and hot-reload gating over the same two build properties.
    private static SingleFileComponentProjectionInput CreateInput(string path, string content, string? configuration)
    {
        var names = SingleFileComponentNameResolver.Resolve(path, ProjectDirectory, RootNamespace);
        var emitHotReloadMetadata = string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase);
        var format = path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)
            ? SingleFileComponentFormat.Vue
            : SingleFileComponentFormat.Viu;
        return new SingleFileComponentProjectionInput(
            format,
            path,
            path.Substring(path.LastIndexOf('/') + 1),
            content,
            names.Namespace,
            names.ClassName,
            names.HintName,
            StyleScopeId.Resolve(path, ProjectDirectory),
            emitHotReloadMetadata
                ? SingleFileComponentHotReloadMetadataFactory.ResolveComponentIdentifier(path, ProjectDirectory)
                : null,
            HasCanonicalPeer: false);
    }

    private static (string Id, string Severity, string Path, int StartLine, int StartCharacter, int EndLine, int EndCharacter, string Message)
        Describe(RoslynDiagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        return (
            diagnostic.Id,
            diagnostic.Severity.ToString(),
            span.Path,
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character,
            diagnostic.GetMessage());
    }

    private static (string Id, string Severity, string Path, int StartLine, int StartCharacter, int EndLine, int EndCharacter, string Message)
        Describe(DiagnosticInfo diagnostic)
        => (
            diagnostic.Descriptor.Id,
            MapSeverity(diagnostic.Descriptor.DefaultSeverity),
            diagnostic.Location.FilePath,
            diagnostic.Location.StartLine,
            diagnostic.Location.StartCharacter,
            diagnostic.Location.EndLine,
            diagnostic.Location.EndCharacter,
            diagnostic.Message);

    // The same neutral-to-Roslyn severity mapping SingleFileComponentDiagnosticAdapter's descriptors
    // encode (pinned 1:1 by SingleFileComponentDiagnosticAdapterTests).
    private static string MapSeverity(SingleFileComponentDiagnosticSeverity severity)
        => severity switch
        {
            SingleFileComponentDiagnosticSeverity.Error => nameof(RoslynDiagnosticSeverity.Error),
            SingleFileComponentDiagnosticSeverity.Warning => nameof(RoslynDiagnosticSeverity.Warning),
            SingleFileComponentDiagnosticSeverity.Information => nameof(RoslynDiagnosticSeverity.Info),
            _ => throw new InvalidOperationException($"The neutral severity '{severity}' has no Roslyn mapping."),
        };
}
