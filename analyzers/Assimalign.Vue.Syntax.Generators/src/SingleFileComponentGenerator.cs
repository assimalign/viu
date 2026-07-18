using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using Assimalign.Vue.Syntax.SingleFileComponent;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The incremental source generator for <c>.viu</c> single-file components — the composition root of the
/// <c>Assimalign.Vue.Syntax.*</c> cluster ([V01.01.06.02]). It is the C# analog of consuming
/// <c>@vue/compiler-sfc</c> through <c>@vitejs/plugin-vue</c>: MSBuild flows every <c>.viu</c> file in as
/// an <see cref="AdditionalText"/>, this generator parses each one with the composed
/// <see cref="SingleFileComponentParserComposition">block + template parser</see>, and emits a partial
/// class scaffold per component. WASM has no runtime <c>new Function</c> template compilation, so
/// build-time generation is Vuecs's only compilation path.
/// <para>
/// Every pipeline stage's input and output is a value-equatable record with no <c>AdditionalText</c>,
/// <c>SourceText</c>, <c>Compilation</c>, or <c>ISymbol</c> captured, so editing one <c>.viu</c> file
/// re-parses and re-emits only that file and untouched files stay cached — the contract the incremental
/// tests pin. The scaffold body is intentionally a shell: the render function is [V01.01.05.05]'s output
/// and the merged <c>@script</c> C# is [V01.01.06.03]'s; this generator emits their seams only.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SingleFileComponentGenerator : IIncrementalGenerator
{
    private const string ViuExtension = ".viu";
    private const string RootNamespaceProperty = "build_property.RootNamespace";
    private const string ProjectDirectoryProperty = "build_property.ProjectDir";

    /// <summary>Pipeline step tracking name for the file-read transform (used by incremental-cache tests).</summary>
    public const string FileTrackingName = "SingleFileComponentFile";

    /// <summary>Pipeline step tracking name for the parse-and-model transform (used by incremental-cache tests).</summary>
    public const string ModelTrackingName = "SingleFileComponentModel";

    // The composed .viu parser is stateless and recoverable, so one shared instance serves every parse.
    private static readonly SingleFileComponentSyntaxParser Parser = SingleFileComponentParserComposition.Create();

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract only the two project properties the names depend on into a value-equatable record, so
        // an unrelated config change does not invalidate the cache.
        var projectOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue(RootNamespaceProperty, out var rootNamespace);
            provider.GlobalOptions.TryGetValue(ProjectDirectoryProperty, out var projectDirectory);
            return new ProjectOptions(rootNamespace, projectDirectory);
        });

        var files = context.AdditionalTextsProvider
            .Where(static text => IsViuFile(text.Path))
            .Combine(projectOptions)
            .Select(static (pair, cancellationToken) => ReadFile(pair.Left, pair.Right, cancellationToken))
            .WithTrackingName(FileTrackingName);

        var results = files
            .Select(static (file, cancellationToken) => BuildResult(file, cancellationToken))
            .WithTrackingName(ModelTrackingName);

        context.RegisterSourceOutput(results, static (production, result) => Execute(production, result));
    }

    private static bool IsViuFile(string path)
        => path.EndsWith(ViuExtension, StringComparison.OrdinalIgnoreCase);

    private static SingleFileComponentFile ReadFile(
        AdditionalText additionalText,
        ProjectOptions options,
        System.Threading.CancellationToken cancellationToken)
    {
        var text = additionalText.GetText(cancellationToken);
        var content = text?.ToString() ?? string.Empty;
        var names = SingleFileComponentNameResolver.Resolve(additionalText.Path, options.ProjectDirectory, options.RootNamespace);
        return new SingleFileComponentFile(
            additionalText.Path,
            LeafFileName(additionalText.Path),
            content,
            names.Namespace,
            names.ClassName,
            names.HintName);
    }

    private static SingleFileComponentGeneratorResult BuildResult(
        SingleFileComponentFile file,
        System.Threading.CancellationToken cancellationToken)
    {
        var parse = Parser.ParseComponent(file.Text, cancellationToken);
        var descriptor = parse.Descriptor;

        var diagnostics = new List<DiagnosticInfo>();

        // Container diagnostics: the .viu block parser reports file-relative positions.
        foreach (var diagnostic in parse.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.Create(file.FilePath, diagnostic, fromTemplate: false, blockContentStart: null));
        }

        // Dispatched-block diagnostics: each registered parser reports positions relative to the block's
        // content, so compose them with the block's content-start position to reach file coordinates.
        foreach (var sourceResult in parse.SourceResults)
        {
            var fromTemplate = SingleFileComponentParserComposition.IsTemplateBlock(sourceResult.Source);
            var blockContentStart = sourceResult.Node.ContentLocation.Start;
            foreach (var diagnostic in sourceResult.Result.Diagnostics)
            {
                diagnostics.Add(SingleFileComponentDiagnostics.Create(file.FilePath, diagnostic, fromTemplate, blockContentStart));
            }
        }

        var model = new SingleFileComponentModel(
            file.Namespace,
            file.ClassName,
            file.FileName,
            file.HintName,
            HasTemplate: descriptor.Template is not null,
            HasScript: descriptor.Script is not null,
            StyleCount: descriptor.Styles.Count,
            CustomBlockCount: descriptor.CustomBlocks.Count);

        var array = diagnostics.Count == 0
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());

        return new SingleFileComponentGeneratorResult(model, array);
    }

    private static void Execute(SourceProductionContext context, SingleFileComponentGeneratorResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        context.AddSource(result.Model.HintName, SingleFileComponentSourceEmitter.Emit(result.Model));
    }

    private static string LeafFileName(string path)
    {
        var separator = path.LastIndexOfAny(PathSeparators);
        return separator >= 0 ? path.Substring(separator + 1) : path;
    }

    private static readonly char[] PathSeparators = { '/', '\\' };
}
