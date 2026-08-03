using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Tooling.Css;
using Assimalign.Viu.Tooling.SingleFileComponent;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The incremental source generator for canonical <c>.viu</c> and compatible tag-based <c>.vue</c>
/// single-file components — the composition root of the
/// <c>Assimalign.Viu.Syntax.*</c> cluster ([V01.01.06.02]). MSBuild globs every component file in as
/// an <see cref="AdditionalText"/>, this generator projects each one through the shared
/// <see cref="SingleFileComponentProjection"/> core ([V01.01.06.11] — the same parse/analyze/compile
/// pipeline the language service runs in the editor), and emits a partial class scaffold per component.
/// WASM has no runtime <c>new Function</c> template compilation, so build-time generation is Viu's only
/// compilation path.
/// <para>
/// Every pipeline stage's input and output is a value-equatable record with no <c>AdditionalText</c>,
/// <c>SourceText</c>, <c>Compilation</c>, or <c>ISymbol</c> captured, so editing one <c>.viu</c> file
/// re-parses and re-emits only that file and untouched files stay cached — the contract the incremental
/// tests pin. This project owns only the host concerns: the incremental pipeline, file reads, hot-reload
/// gating, <c>.vue</c>-shadowing (a multi-file MSBuild concern), and the Roslyn materialization of the
/// projection's neutral diagnostics (<see cref="SingleFileComponentDiagnosticAdapter"/>).
/// </para>
/// <para>
/// For Debug builds, or when explicitly enabled by <c>ViuEmitHotReloadMetadata</c>, the generator also
/// emits [V01.01.06.05]'s path-stable component identity, independent template/script/style hashes, and
/// one consumer-assembly <c>MetadataUpdateHandler</c> that forwards applied deltas to Core. Ordinary
/// Release builds omit that entire development surface.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SingleFileComponentGenerator : IIncrementalGenerator
{
    private const string ViuExtension = ".viu";
    private const string VueExtension = ".vue";
    private const string RootNamespaceProperty = "build_property.RootNamespace";
    private const string ProjectDirectoryProperty = "build_property.ProjectDir";
    private const string ConfigurationProperty = "build_property.Configuration";
    private const string EmitHotReloadMetadataProperty = "build_property.ViuEmitHotReloadMetadata";

    /// <summary>Pipeline step tracking name for the file-read transform (used by incremental-cache tests).</summary>
    public const string FileTrackingName = "SingleFileComponentFile";

    /// <summary>Pipeline step tracking name for the parse-and-model transform (used by incremental-cache tests).</summary>
    public const string ModelTrackingName = "SingleFileComponentModel";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract only the two project properties the names depend on into a value-equatable record, so
        // an unrelated config change does not invalidate the cache.
        var projectOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue(RootNamespaceProperty, out var rootNamespace);
            provider.GlobalOptions.TryGetValue(ProjectDirectoryProperty, out var projectDirectory);
            provider.GlobalOptions.TryGetValue(ConfigurationProperty, out var configuration);
            provider.GlobalOptions.TryGetValue(EmitHotReloadMetadataProperty, out var emitHotReloadMetadata);
            return new ProjectOptions(
                rootNamespace,
                projectDirectory,
                ShouldEmitHotReloadMetadata(configuration, emitHotReloadMetadata));
        });

        var canonicalFileKeys = context.AdditionalTextsProvider
            .Where(static text => IsViuFile(text.Path))
            .Select(static (text, _) => ComponentBasePath(text.Path))
            .Collect();

        var files = context.AdditionalTextsProvider
            .Where(static text => IsSingleFileComponentFile(text.Path))
            .Combine(projectOptions)
            .Combine(canonicalFileKeys)
            .Select(static (pair, cancellationToken) => ReadFile(
                pair.Left.Left,
                pair.Left.Right,
                pair.Right,
                cancellationToken))
            .WithTrackingName(FileTrackingName);

        var results = files
            .Where(static file => !file.HasCanonicalPeer)
            .Select(static (file, cancellationToken) =>
                SingleFileComponentProjection.Project(file, cancellationToken))
            .WithTrackingName(ModelTrackingName);

        var collisions = files
            .Where(static file => file.HasCanonicalPeer)
            .Select(static (file, _) => SingleFileComponentDiagnostics.CreateFileRule(
                SingleFileComponentDiagnostics.ConflictingComponentFormats,
                "The compatibility .vue source is shadowed by a same-directory, same-base .viu source. " +
                "The canonical .viu component takes precedence.",
                file.FilePath));

        context.RegisterSourceOutput(results, static (production, result) => Execute(production, result));
        context.RegisterSourceOutput(collisions, static (production, diagnostic) =>
            production.ReportDiagnostic(SingleFileComponentDiagnosticAdapter.ToDiagnostic(diagnostic)));
        context.RegisterSourceOutput(
            projectOptions,
            static (production, options) =>
            {
                if (options.EmitHotReloadMetadata)
                {
                    production.AddSource(
                        SingleFileComponentHotReloadHandlerEmitter.HintName,
                        SingleFileComponentHotReloadHandlerEmitter.Emit());
                }
            });
    }

    private static bool IsSingleFileComponentFile(string path)
        => IsViuFile(path) || IsVueFile(path);

    private static bool IsViuFile(string path)
        => path.EndsWith(ViuExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsVueFile(string path)
        => path.EndsWith(VueExtension, StringComparison.OrdinalIgnoreCase);

    private static SingleFileComponentProjectionInput ReadFile(
        AdditionalText additionalText,
        ProjectOptions options,
        ImmutableArray<string> canonicalFileKeys,
        System.Threading.CancellationToken cancellationToken)
    {
        var text = additionalText.GetText(cancellationToken);
        var content = text?.ToString() ?? string.Empty;
        var names = SingleFileComponentNameResolver.Resolve(additionalText.Path, options.ProjectDirectory, options.RootNamespace);
        var scopeId = StyleScopeId.Resolve(additionalText.Path, options.ProjectDirectory);
        var format = IsVueFile(additionalText.Path)
            ? SingleFileComponentFormat.Vue
            : SingleFileComponentFormat.Viu;
        return new SingleFileComponentProjectionInput(
            format,
            additionalText.Path,
            LeafFileName(additionalText.Path),
            content,
            names.Namespace,
            names.ClassName,
            names.HintName,
            scopeId,
            options.EmitHotReloadMetadata
                ? SingleFileComponentHotReloadMetadataFactory.ResolveComponentIdentifier(
                    additionalText.Path,
                    options.ProjectDirectory)
                : null,
            HasCanonicalPeer(format, additionalText.Path, canonicalFileKeys));
    }

    private static bool ShouldEmitHotReloadMetadata(
        string? configuration,
        string? configuredValue)
    {
        if (!string.IsNullOrEmpty(configuredValue))
        {
            return string.Equals(configuredValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCanonicalPeer(
        SingleFileComponentFormat format,
        string filePath,
        ImmutableArray<string> canonicalFileKeys)
    {
        if (format != SingleFileComponentFormat.Vue)
        {
            return false;
        }

        var key = ComponentBasePath(filePath);
        foreach (var canonicalKey in canonicalFileKeys)
        {
            if (string.Equals(
                    key,
                    canonicalKey,
                    SingleFileComponentPathComparison.Comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComponentBasePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Length >= ViuExtension.Length
            ? normalized.Substring(0, normalized.Length - ViuExtension.Length)
            : normalized;
    }

    private static void Execute(SourceProductionContext context, SingleFileComponentProjectionResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(SingleFileComponentDiagnosticAdapter.ToDiagnostic(diagnostic));
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
