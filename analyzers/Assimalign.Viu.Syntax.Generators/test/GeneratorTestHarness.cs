using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

// The test namespace is nested under Assimalign.Viu.Syntax, so the base cluster's Diagnostic type is
// ambient and shadows Roslyn's; alias the Roslyn diagnostics the generator reports.
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;

namespace Assimalign.Viu.Syntax.Generators.Tests;

/// <summary>
/// Drives <see cref="SingleFileComponentGenerator"/> over in-memory <c>.viu</c> additional files for
/// snapshot, diagnostic, and incremental-cache tests. This is the Roslyn source-generator testing
/// harness the work item calls for: it feeds the generator exactly the inputs MSBuild would — a
/// <c>.viu</c> <see cref="AdditionalText"/> plus the <c>RootNamespace</c>/<c>ProjectDir</c> build
/// properties — and exposes the run result and step-tracking for cache assertions.
/// </summary>
internal static class GeneratorTestHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    /// <summary>Builds an empty library compilation (the generator does not read the compilation).</summary>
    /// <param name="sources">Optional C# sources to include (used to add unrelated trees for cache tests).</param>
    /// <returns>The compilation.</returns>
    internal static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var trees = new List<SyntaxTree>();
        foreach (var source in sources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(source, ParseOptions));
        }

        return CSharpCompilation.Create(
            "Assimalign.Viu.Syntax.Generators.TestAssembly",
            trees,
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Parses an unrelated C# source tree for the unrelated-edit cache test.</summary>
    /// <param name="source">The C# source text.</param>
    /// <returns>The parsed syntax tree.</returns>
    internal static SyntaxTree Parse(string source) => CSharpSyntaxTree.ParseText(source, ParseOptions);

    /// <summary>Creates a step-tracking driver over the generator for the given <c>.viu</c> files.</summary>
    /// <param name="files">The in-memory <c>.viu</c> additional files.</param>
    /// <param name="rootNamespace">The <c>RootNamespace</c> build property, or <see langword="null"/>.</param>
    /// <param name="projectDirectory">The <c>ProjectDir</c> build property, or <see langword="null"/>.</param>
    /// <returns>The driver.</returns>
    internal static GeneratorDriver CreateDriver(
        ImmutableArray<AdditionalText> files,
        string? rootNamespace,
        string? projectDirectory)
    {
        var globalOptions = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rootNamespace is not null)
        {
            globalOptions["build_property.RootNamespace"] = rootNamespace;
        }
        if (projectDirectory is not null)
        {
            globalOptions["build_property.ProjectDir"] = projectDirectory;
        }

        return CSharpGeneratorDriver.Create(
            generators: new[] { new SingleFileComponentGenerator().AsSourceGenerator() },
            additionalTexts: files,
            parseOptions: ParseOptions,
            optionsProvider: new InMemoryAnalyzerConfigOptionsProvider(new InMemoryAnalyzerConfigOptions(globalOptions)),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    /// <summary>Runs the generator once over a single <c>.viu</c> file and returns the run outcome.</summary>
    /// <param name="path">The <c>.viu</c> file path.</param>
    /// <param name="content">The <c>.viu</c> source text.</param>
    /// <param name="rootNamespace">The <c>RootNamespace</c> build property.</param>
    /// <param name="projectDirectory">The <c>ProjectDir</c> build property.</param>
    /// <returns>The generated sources and reported diagnostics.</returns>
    internal static GeneratorOutcome Run(string path, string content, string? rootNamespace = null, string? projectDirectory = null)
    {
        var file = new InMemoryAdditionalText(path, content);
        var driver = CreateDriver(ImmutableArray.Create<AdditionalText>(file), rootNamespace, projectDirectory)
            .RunGenerators(CreateCompilation());
        var result = driver.GetRunResult().Results[0];
        return new GeneratorOutcome(result.GeneratedSources, result.Diagnostics);
    }

    /// <summary>The generated source whose hint name ends with <paramref name="hintSuffix"/>, with LF endings.</summary>
    /// <param name="outcome">A run outcome.</param>
    /// <param name="hintSuffix">The trailing hint-name fragment (e.g. "Counter.SingleFileComponent.g.cs").</param>
    /// <returns>The generated source text with normalized (LF) line endings.</returns>
    internal static string GeneratedSource(GeneratorOutcome outcome, string hintSuffix)
    {
        foreach (var source in outcome.Sources)
        {
            if (source.HintName.EndsWith(hintSuffix, StringComparison.Ordinal))
            {
                return source.SourceText.ToString().Replace("\r\n", "\n");
            }
        }

        throw new InvalidOperationException($"No generated source with hint name ending in '{hintSuffix}'.");
    }
}

/// <summary>The outcome of one generator run: the generated sources and the reported diagnostics.</summary>
internal readonly struct GeneratorOutcome
{
    internal GeneratorOutcome(ImmutableArray<GeneratedSourceResult> sources, ImmutableArray<RoslynDiagnostic> diagnostics)
    {
        Sources = sources;
        Diagnostics = diagnostics;
    }

    /// <summary>The generated source files.</summary>
    internal ImmutableArray<GeneratedSourceResult> Sources { get; }

    /// <summary>The diagnostics reported by the generator.</summary>
    internal ImmutableArray<RoslynDiagnostic> Diagnostics { get; }
}

/// <summary>An in-memory <c>.viu</c> additional file with a fixed path and content.</summary>
internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    internal InMemoryAdditionalText(string path, string content)
    {
        Path = path;
        _text = SourceText.From(content);
    }

    /// <inheritdoc />
    public override string Path { get; }

    /// <inheritdoc />
    public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => _text;
}

/// <summary>An in-memory <see cref="AnalyzerConfigOptions"/> over a fixed key/value map.</summary>
internal sealed class InMemoryAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values;

    internal InMemoryAnalyzerConfigOptions(Dictionary<string, string> values) => _values = values;

    /// <inheritdoc />
    public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
}

/// <summary>An <see cref="AnalyzerConfigOptionsProvider"/> that returns one set of global options everywhere.</summary>
internal sealed class InMemoryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _options;

    internal InMemoryAnalyzerConfigOptionsProvider(AnalyzerConfigOptions options) => _options = options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
}
