using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Reactivity.Generators.Tests;

/// <summary>
/// Drives <see cref="ReactiveGenerator"/> over in-memory source for snapshot, diagnostic, and
/// incremental-cache tests. Builds a compilation referencing the real runtime library so
/// <c>[Reactive]</c> and <c>IReactiveObject</c> resolve exactly as they do for a consumer.
/// </summary>
internal static class GeneratorTestHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    /// <summary>Parses <paramref name="source"/> with the same options the harness compiles with.</summary>
    /// <param name="source">The C# source text.</param>
    /// <returns>The parsed syntax tree.</returns>
    internal static SyntaxTree Parse(string source) => CSharpSyntaxTree.ParseText(source, ParseOptions);

    /// <summary>Parses <paramref name="sources"/> into a nullable-enabled library compilation.</summary>
    /// <param name="sources">The C# source texts.</param>
    /// <returns>The compilation.</returns>
    internal static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source, ParseOptions));
        return CSharpCompilation.Create(
            "Assimalign.Viu.Reactivity.Generators.TestAssembly",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Creates a step-tracking driver over the generator.</summary>
    /// <returns>The driver.</returns>
    internal static GeneratorDriver CreateDriver()
        => CSharpGeneratorDriver.Create(
            generators: new[] { new ReactiveGenerator().AsSourceGenerator() },
            additionalTexts: ImmutableArray<AdditionalText>.Empty,
            parseOptions: ParseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    /// <summary>Runs the generator once over <paramref name="source"/> and returns the run outcome.</summary>
    /// <param name="source">The C# source text.</param>
    /// <returns>The generated sources and reported diagnostics.</returns>
    internal static GeneratorOutcome Run(string source)
    {
        var driver = CreateDriver().RunGenerators(CreateCompilation(source));
        var result = driver.GetRunResult().Results[0];
        return new GeneratorOutcome(result.GeneratedSources, result.Diagnostics);
    }

    /// <summary>The generated source whose hint name ends with <paramref name="hintSuffix"/>.</summary>
    /// <param name="outcome">A run outcome.</param>
    /// <param name="hintSuffix">The trailing hint-name fragment (e.g. "TodoItem.Reactive.g.cs").</param>
    /// <returns>The generated source text with normalized (LF) line endings.</returns>
    internal static string GeneratedSource(GeneratorOutcome outcome, string hintSuffix)
    {
        var generated = outcome.Sources.Single(source => source.HintName.EndsWith(hintSuffix, StringComparison.Ordinal));
        return generated.SourceText.ToString().Replace("\r\n", "\n");
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in platform.Split(Path.PathSeparator))
        {
            if (path.Length > 0 && File.Exists(path) && seen.Add(path))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }
        var reactivity = typeof(ReactiveAttribute).Assembly.Location;
        if (seen.Add(reactivity))
        {
            builder.Add(MetadataReference.CreateFromFile(reactivity));
        }
        return builder.ToImmutable();
    }
}

/// <summary>The outcome of one generator run: the generated sources and the reported diagnostics.</summary>
internal readonly struct GeneratorOutcome
{
    internal GeneratorOutcome(ImmutableArray<GeneratedSourceResult> sources, ImmutableArray<Diagnostic> diagnostics)
    {
        Sources = sources;
        Diagnostics = diagnostics;
    }

    /// <summary>The generated source files.</summary>
    internal ImmutableArray<GeneratedSourceResult> Sources { get; }

    /// <summary>The diagnostics reported by the generator.</summary>
    internal ImmutableArray<Diagnostic> Diagnostics { get; }
}
