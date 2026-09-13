using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Assimalign.Viu.Generators.FileRouting.Tests;

internal static class GeneratorTestHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    internal static CSharpCompilation Compilation(params string[] sources) => CSharpCompilation.Create("FileRouting.TestAssembly",
        sources.Select(source => CSharpSyntaxTree.ParseText(source, ParseOptions)),
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    internal static GeneratorDriver Driver(IEnumerable<AdditionalText> files, Dictionary<string, string>? properties = null)
        => CSharpGeneratorDriver.Create(new[] { new FileRoutingGenerator().AsSourceGenerator() }, files,
            ParseOptions, Options(properties), new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));

    internal static AnalyzerConfigOptionsProvider Options(Dictionary<string, string>? properties = null)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.ProjectDir"] = "/project/",
            ["build_property.RootNamespace"] = "Example.Application",
            ["build_property.ViuFileRoutingEnabled"] = "true",
            ["build_property.ViuFileRoutingPagesDirectory"] = "Pages",
        };
        if (properties is not null)
        {
            foreach (var pair in properties)
            {
                values["build_property." + pair.Key] = pair.Value;
            }
        }
        return new InMemoryOptionsProvider(values);
    }

    internal static GeneratorRunResult Run(params AdditionalText[] files)
        => Driver(files).RunGenerators(Compilation()).GetRunResult().Results.Single();

    internal static string Source(GeneratorRunResult result) => result.GeneratedSources.Single().SourceText.ToString();

    internal static InMemoryAdditionalText Page(string path, string? content = null)
        => new("/project/Pages/" + path, content ?? "<template><div /></template>\n");

    internal static ImmutableArray<string> DiagnosticIdentifiers(GeneratorRunResult result)
        => result.Diagnostics.Select(diagnostic => diagnostic.Id).ToImmutableArray();
}

internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText text;

    internal InMemoryAdditionalText(string path, string content)
    {
        Path = path;
        text = SourceText.From(content);
    }

    public override string Path { get; }
    public override SourceText GetText(CancellationToken cancellationToken = default) => text;
}

internal sealed class InMemoryOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions options;

    internal InMemoryOptionsProvider(Dictionary<string, string> properties) => options = new InMemoryOptions(properties);
    public override AnalyzerConfigOptions GlobalOptions => options;
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
}

internal sealed class InMemoryOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> properties;

    internal InMemoryOptions(Dictionary<string, string> properties) => this.properties = properties;
    public override bool TryGetValue(string key, out string value) => properties.TryGetValue(key, out value!);
}
