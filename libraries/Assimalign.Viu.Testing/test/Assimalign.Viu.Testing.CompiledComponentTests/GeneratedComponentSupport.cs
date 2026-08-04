using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Shouldly;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Generators.Syntax;

namespace Assimalign.Viu.Testing.Tests;

/// <summary>
/// Compiles a real <c>.viu</c> source through the real <see cref="SingleFileComponentGenerator"/> and
/// loads the emitted partial class as an <see cref="IComponentTemplate"/>, so a test can mount the
/// generated component in the in-memory host and assert what a developer would actually observe. The
/// generator and Roslyn appear only here, never in runtime code; the compiled component is pure runtime
/// C# bound against <c>Assimalign.Viu.Core</c> and <c>Assimalign.Viu.Components</c>.
/// </summary>
internal static class GeneratedComponentSupport
{
    private const string RootNamespace = "Demo";
    private const string ProjectDirectory = "C:/proj";

    /// <summary>
    /// Generates, compiles, and loads <paramref name="source"/> as component
    /// <paramref name="componentName"/>, returning an activator for fresh instances.
    /// </summary>
    /// <param name="componentName">The component name (its <c>.viu</c> file base name and class name).</param>
    /// <param name="source">The <c>.viu</c> source text.</param>
    /// <returns>An activator producing a fresh template instance per mount.</returns>
    internal static ComponentActivator Compile(string componentName, string source)
    {
        var type = CompileToType(componentName, source);
        return () => (IComponentTemplate)Activator.CreateInstance(type)!;
    }

    /// <summary>Generates, compiles, and loads the component, returning its runtime <see cref="Type"/>.</summary>
    /// <param name="componentName">The component name.</param>
    /// <param name="source">The <c>.viu</c> source text.</param>
    /// <param name="requireNoWarnings">
    /// Whether the compiled scaffold must be warning-free. Pass <see langword="false"/> only when the
    /// warning is itself the behavior under test — a component that hides an inherited member
    /// ([CMP-32]), for example.
    /// </param>
    /// <returns>The loaded component type.</returns>
    internal static Type CompileToType(string componentName, string source, bool requireNoWarnings = true)
    {
        var generated = Generate(componentName, source);
        var assembly = Assembly.Load(CompileToAssembly(generated, requireNoWarnings));
        return assembly.GetType($"{RootNamespace}.{componentName}")
            ?? throw new InvalidOperationException(
                $"The compiled assembly did not contain {RootNamespace}.{componentName}.");
    }

    /// <summary>
    /// Generates and compiles the component, returning the C# error and warning ids the compilation
    /// reported. This is the shape a test needs when the compiler's own diagnostic is the subject.
    /// </summary>
    /// <param name="componentName">The component name.</param>
    /// <param name="source">The <c>.viu</c> source text.</param>
    /// <returns>The reported diagnostic ids, in source order.</returns>
    internal static IReadOnlyList<string> CompileDiagnostics(string componentName, string source)
    {
        return CreateCompilation(Generate(componentName, source))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.Id)
            .ToList();
    }

    /// <summary>Runs the generator over one <c>.viu</c> source and returns the generated partial-class text.</summary>
    /// <param name="componentName">The component name.</param>
    /// <param name="source">The <c>.viu</c> source text.</param>
    /// <returns>The generated C# source.</returns>
    internal static string Generate(string componentName, string source)
    {
        var file = new InMemoryComponentText($"{ProjectDirectory}/{componentName}.viu", source);
        var globalOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = RootNamespace,
            ["build_property.ProjectDir"] = ProjectDirectory,
        };
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new SingleFileComponentGenerator().AsSourceGenerator() },
            additionalTexts: ImmutableArray.Create<AdditionalText>(file),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            optionsProvider: new InMemoryComponentOptionsProvider(globalOptions));

        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
        return result.GeneratedSources
            .Single(generated => generated.HintName.EndsWith(
                $"{componentName}.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .SourceText.ToString();
    }

    private static CSharpCompilation CreateCompilation(string generatedSource)
    {
        return CSharpCompilation.Create(
            "Demo.Compiled." + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(generatedSource, new CSharpParseOptions(LanguageVersion.Preview)) },
            ResolveReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static byte[] CompileToAssembly(string generatedSource, bool requireNoWarnings)
    {
        var compilation = CreateCompilation(generatedSource);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        if (!emit.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                emit.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException(
                "The generated component scaffold failed to compile:"
                + Environment.NewLine + errors + Environment.NewLine + Environment.NewLine + generatedSource);
        }

        // The scaffold is held to the same bar as the rest of the repository: a generated component that
        // warns would make a consumer's zero-warning build impossible to keep.
        if (requireNoWarnings)
        {
            emit.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
                .Select(diagnostic => diagnostic.ToString())
                .ShouldBeEmpty();
        }

        return stream.ToArray();
    }

    private static IReadOnlyList<MetadataReference> ResolveReferences()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();
        void Add(string? location)
        {
            if (!string.IsNullOrEmpty(location) && seen.Add(location!))
            {
                references.Add(MetadataReference.CreateFromFile(location!));
            }
        }

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    Add(path);
                }
            }
        }

        Add(typeof(object).Assembly.Location);
        Add(typeof(RenderHelpers).Assembly.Location);
        Add(typeof(IComponentTemplate).Assembly.Location);
        return references;
    }
}

/// <summary>An in-memory <c>.viu</c> additional file for the source generator.</summary>
internal sealed class InMemoryComponentText : AdditionalText
{
    private readonly SourceText _text;

    internal InMemoryComponentText(string path, string content)
    {
        Path = path;
        _text = SourceText.From(content);
    }

    /// <inheritdoc />
    public override string Path { get; }

    /// <inheritdoc />
    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}

/// <summary>An <see cref="AnalyzerConfigOptions"/> over a fixed global build-property map.</summary>
internal sealed class InMemoryComponentOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values;

    internal InMemoryComponentOptions(Dictionary<string, string> values) => _values = values;

    /// <inheritdoc />
    public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
}

/// <summary>An <see cref="AnalyzerConfigOptionsProvider"/> returning one global options set everywhere.</summary>
internal sealed class InMemoryComponentOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _options;

    internal InMemoryComponentOptionsProvider(Dictionary<string, string> values)
        => _options = new InMemoryComponentOptions(values);

    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
}
