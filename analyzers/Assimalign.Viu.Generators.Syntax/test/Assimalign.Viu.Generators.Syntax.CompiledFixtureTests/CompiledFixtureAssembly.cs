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
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Generators.Syntax;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Generators.Syntax.CompiledFixtureTests;

/// <summary>
/// Runs the production incremental generator over physical fixture files and loads its output as
/// one transient assembly compiled only against the shipping runtime.
/// </summary>
internal sealed class CompiledFixtureAssembly
{
    private const string FixtureNamespace = "ViuCompiledFixtures";
    private static readonly Lazy<CompiledFixtureAssembly> Shared = new(Compile);

    private CompiledFixtureAssembly(
        Assembly assembly,
        IReadOnlyDictionary<string, string> generatedSources)
    {
        Assembly = assembly;
        GeneratedSources = generatedSources;
    }

    internal static CompiledFixtureAssembly Instance => Shared.Value;

    internal Assembly Assembly { get; }

    internal IReadOnlyDictionary<string, string> GeneratedSources { get; }

    internal Type GetComponentType(string name) =>
        Assembly.GetType(string.Concat(FixtureNamespace, ".", name))
        ?? throw new InvalidOperationException(
            $"The compiled fixture assembly does not contain component '{name}'.");

    internal void RegisterComponents(ComponentFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Type catalog = Assembly.GetType(
            string.Concat(FixtureNamespace, ".GeneratedViuComponents"))
            ?? throw new InvalidOperationException(
                "The generated assembly catalog was not emitted.");
        MethodInfo register = catalog.GetMethod(
            "Register",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(ComponentFactory)],
            modifiers: null)
            ?? throw new InvalidOperationException(
                "GeneratedViuComponents.Register(ComponentFactory) was not emitted.");
        register.Invoke(null, [factory]);
    }

    private static CompiledFixtureAssembly Compile()
    {
        string[] fixtureNames =
        [
            // Verbatim physical copies of the shipping compiled-component source constants.
            "AttributedCard.viu",
            "Rating.viu",
            "PatchProbe.viu",
            "TargetedTextProbe.viu",
            "InteractionProbe.viu",
            "NullableParameterProbe.viu",
            "SlotChild.viu",
            "SlotOwner.viu",
            "MixedAuthoring.viu",
            "VueProbe.vue",
        ];
        var additionalFiles = ImmutableArray.CreateBuilder<AdditionalText>(fixtureNames.Length);
        foreach (string fixtureName in fixtureNames)
        {
            string physicalPath = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                fixtureName);
            additionalFiles.Add(
                new PhysicalFixtureText(
                    string.Concat("C:/fixture/", fixtureName),
                    File.ReadAllText(physicalPath)));
        }

        var globalOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = FixtureNamespace,
            ["build_property.ProjectDir"] = "C:/fixture/",
            ["build_property.Configuration"] = "Debug",
            ["build_property.ViuEmitHotReloadMetadata"] = "true",
        };
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new SingleFileComponentGenerator().AsSourceGenerator()],
            additionalTexts: additionalFiles.ToImmutable(),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            optionsProvider: new FixtureAnalyzerConfigOptionsProvider(globalOptions));
        CSharpCompilation input = CSharpCompilation.Create(
            "ViuCompiledFixtureGeneratorInput",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriverRunResult run = driver.RunGenerators(input).GetRunResult();
        GeneratorRunResult generator = run.Results.Single();
        ThrowForDiagnostics("generator", generator.Diagnostics);

        var generatedSources = new Dictionary<string, string>(StringComparer.Ordinal);
        var syntaxTrees = new List<SyntaxTree>(generator.GeneratedSources.Length);
        foreach (GeneratedSourceResult generated in generator.GeneratedSources)
        {
            string source = generated.SourceText.ToString().Replace("\r\n", "\n");
            generatedSources.Add(generated.HintName, source);
            syntaxTrees.Add(
                CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.Preview),
                    generated.HintName));
        }

        PersistGeneratedSources(generatedSources);

        CSharpCompilation compilation = CSharpCompilation.Create(
            string.Concat("ViuCompiledFixtures_", Guid.NewGuid().ToString("N")),
            syntaxTrees,
            ResolveRuntimeReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));
        using var assemblyStream = new MemoryStream();
        EmitResult emit = compilation.Emit(assemblyStream);
        ThrowForDiagnostics("generated compilation", emit.Diagnostics);
        if (!emit.Success)
        {
            throw new InvalidOperationException(
                "The generated fixture compilation failed without an error diagnostic.");
        }

        return new CompiledFixtureAssembly(
            Assembly.Load(assemblyStream.ToArray()),
            generatedSources);
    }

    private static IReadOnlyList<MetadataReference> ResolveRuntimeReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (string path in trusted.Split(Path.PathSeparator))
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string assemblyName = Path.GetFileNameWithoutExtension(path);
                    if (!assemblyName.StartsWith(
                            "Assimalign.Viu.",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(path);
                    }
                }
            }
        }

        paths.Add(typeof(ComponentBase).Assembly.Location);
        paths.Add(typeof(Renderer<>).Assembly.Location);
        paths.Add(typeof(Reactive).Assembly.Location);
        paths.Add(typeof(ModelBinding).Assembly.Location);
        return paths
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static void PersistGeneratedSources(
        IReadOnlyDictionary<string, string> generatedSources)
    {
        string generatedDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Generated");
        Directory.CreateDirectory(generatedDirectory);
        foreach (KeyValuePair<string, string> generated in generatedSources)
        {
            File.WriteAllText(
                Path.Combine(generatedDirectory, Path.GetFileName(generated.Key)),
                generated.Value);
        }
    }

    private static void ThrowForDiagnostics(
        string stage,
        IEnumerable<Diagnostic> diagnostics)
    {
        string[] failures = diagnostics
            .Where(diagnostic => diagnostic.Severity is
                DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();
        if (failures.Length != 0)
        {
            throw new InvalidOperationException(
                string.Concat(
                    "The ",
                    stage,
                    " produced diagnostics:",
                    Environment.NewLine,
                    string.Join(Environment.NewLine, failures)));
        }
    }
}

/// <summary>An in-memory Roslyn input whose text originated from a physical fixture file.</summary>
internal sealed class PhysicalFixtureText : AdditionalText
{
    private readonly SourceText _text;

    internal PhysicalFixtureText(string path, string text)
    {
        Path = path;
        _text = SourceText.From(text);
    }

    /// <inheritdoc />
    public override string Path { get; }

    /// <inheritdoc />
    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}

/// <summary>Supplies the build properties that gate source-generator emission.</summary>
internal sealed class FixtureAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _options;

    internal FixtureAnalyzerConfigOptionsProvider(Dictionary<string, string> values)
    {
        _options = new FixtureAnalyzerConfigOptions(values);
    }

    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
}

/// <summary>Provides immutable analyzer configuration values.</summary>
internal sealed class FixtureAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values;

    internal FixtureAnalyzerConfigOptions(Dictionary<string, string> values)
    {
        _values = values;
    }

    /// <inheritdoc />
    public override bool TryGetValue(string key, out string value) =>
        _values.TryGetValue(key, out value!);
}
