using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Compiler.Css;
using Assimalign.Viu.Compiler.SingleFileComponent;

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
/// Hint-name identity is the other multi-file concern ([SFC-CG-5], [V01.01.06.10.01]). Roslyn compares
/// <c>AddSource</c> hint names case-insensitively and kills the whole generator run on a duplicate, so
/// wherever path identity is ordinal — every non-Windows filesystem [VUE-7] — two components whose base
/// names differ only by case would otherwise claim one hint name. <see cref="SingleFileComponentFileSet"/>
/// resolves those groups once per file set and each member takes the resolver's path-hash discriminator;
/// a component that collides with nothing keeps its readable hint name byte for byte, so no existing
/// generated-file identity moves.
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

        // The multi-file view: .vue shadowing [VUE-7] and hint-name case collisions [SFC-CG-5] are both
        // facts about the SET of component files, so they are resolved once per set — keyed on the
        // project directory alone, never on the whole option record, so an unrelated property change
        // cannot invalidate it.
        var fileSet = context.AdditionalTextsProvider
            .Where(static text => IsSingleFileComponentFile(text.Path))
            .Select(static (text, _) => text.Path)
            .Collect()
            .Combine(projectOptions.Select(static (options, _) => options.ProjectDirectory))
            .Select(static (pair, _) => CreateFileSet(pair.Left, pair.Right));

        var files = context.AdditionalTextsProvider
            .Where(static text => IsSingleFileComponentFile(text.Path))
            .Combine(projectOptions)
            .Combine(fileSet)
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

        // [SFC-USE-1] The component declarations this compilation can read: the attribute-declared
        // surfaces of the .viu components being generated here, plus everything Roslyn can see through
        // symbols (hand-authored components in this compilation, and components in referenced
        // assemblies, whose [Parameter] attributes survive into metadata).
        //
        // The join is deliberately a SEPARATE output branch. The model pipeline above stays uncombined,
        // so an edit to one .viu still re-emits only that file and every other file's scaffold stays
        // strictly cached; only the validation branch — which produces diagnostics, never source — sees
        // the compilation-wide catalog.
        var localDeclarations = results
            .Select(static (result, _) => new ComponentDeclarationEntry(
                result.Model.ClassName,
                result.Model.Declarations.Parameters))
            .Where(static entry => entry.Parameters.Count > 0)
            .Collect();
        var symbolDeclarations = context.CompilationProvider.Select(
            static (compilation, cancellationToken) =>
                ComponentSymbolCatalogReader.Read(compilation, cancellationToken));
        var catalog = localDeclarations
            .Combine(symbolDeclarations)
            .Select(static (pair, _) => BuildCatalog(pair.Left, pair.Right));

        context.RegisterSourceOutput(results, static (production, result) => Execute(production, result));
        context.RegisterSourceOutput(
            results.Combine(catalog),
            static (production, pair) => ValidateComponentUsages(production, pair.Left, pair.Right));
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
        SingleFileComponentFileSet fileSet,
        System.Threading.CancellationToken cancellationToken)
    {
        var text = additionalText.GetText(cancellationToken);
        var content = text?.ToString() ?? string.Empty;
        var names = SingleFileComponentNameResolver.Resolve(
            additionalText.Path,
            options.ProjectDirectory,
            options.RootNamespace,
            fileSet.RequiresCaseDiscriminator(additionalText.Path));
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
            HasCanonicalPeer(format, additionalText.Path, fileSet));
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

    // Resolves the two cross-file facts for one compilation's component files: the canonical .viu base
    // paths a .vue peer is shadowed by [VUE-7], and the components whose readable hint names would
    // collide under Roslyn's case-insensitive AddSource comparison [SFC-CG-5]. Shadowed .vue files are
    // excluded from the collision input because they emit nothing — counting one would discriminate the
    // canonical .viu component that suppressed it, churning an identity that never collided.
    private static SingleFileComponentFileSet CreateFileSet(
        ImmutableArray<string> componentPaths,
        string? projectDirectory)
    {
        if (componentPaths.IsDefaultOrEmpty)
        {
            return SingleFileComponentFileSet.Empty;
        }

        var orderedPaths = new string[componentPaths.Length];
        componentPaths.CopyTo(orderedPaths);
        Array.Sort(orderedPaths, StringComparer.Ordinal);

        var canonicalBasePaths = new List<string>();
        foreach (var path in orderedPaths)
        {
            if (IsViuFile(path))
            {
                canonicalBasePaths.Add(ComponentBasePath(path));
            }
        }

        var canonicalSet = new SingleFileComponentFileSet(
            new EquatableArray<string>(canonicalBasePaths.ToArray()),
            EquatableArray<string>.Empty);

        var emittedPaths = new List<string>(orderedPaths.Length);
        foreach (var path in orderedPaths)
        {
            var format = IsVueFile(path) ? SingleFileComponentFormat.Vue : SingleFileComponentFormat.Viu;
            if (!HasCanonicalPeer(format, path, canonicalSet))
            {
                emittedPaths.Add(path);
            }
        }

        return new SingleFileComponentFileSet(
            canonicalSet.CanonicalBasePaths,
            new EquatableArray<string>(
                SingleFileComponentNameResolver.SelectCaseCollidingPaths(emittedPaths, projectDirectory)));
    }

    private static bool HasCanonicalPeer(
        SingleFileComponentFormat format,
        string filePath,
        SingleFileComponentFileSet fileSet)
        => format == SingleFileComponentFormat.Vue &&
            fileSet.ContainsCanonicalBasePath(ComponentBasePath(filePath));

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

    // Merges the two declaration sources into one resolvable catalog. The .viu components being generated
    // in THIS compilation cannot be read through symbols — their partial classes do not exist yet — so
    // their projections supply them directly.
    private static ComponentDeclarationCatalog BuildCatalog(
        ImmutableArray<ComponentDeclarationEntry> local,
        EquatableArray<ComponentDeclarationEntry> fromSymbols)
    {
        if (local.Length == 0 && fromSymbols.Count == 0)
        {
            return ComponentDeclarationCatalog.Empty;
        }

        var entries = new ComponentDeclarationEntry[local.Length + fromSymbols.Count];
        var index = 0;
        foreach (var entry in local)
        {
            entries[index++] = entry;
        }

        foreach (var entry in fromSymbols)
        {
            entries[index++] = entry;
        }

        return new ComponentDeclarationCatalog(new EquatableArray<ComponentDeclarationEntry>(entries));
    }

    // [SFC-USE-2] Reports the component usages this template gets wrong. Diagnostics only — never source
    // — so the emitted scaffold never depends on the compilation-wide catalog.
    private static void ValidateComponentUsages(
        SourceProductionContext context,
        SingleFileComponentProjectionResult result,
        ComponentDeclarationCatalog catalog)
    {
        if (result.ComponentUsages.Count == 0 || catalog.IsEmpty)
        {
            return;
        }

        var diagnostics = new System.Collections.Generic.List<DiagnosticInfo>();
        ComponentUsageValidator.Validate(result.ComponentUsages, catalog, diagnostics);
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(SingleFileComponentDiagnosticAdapter.ToDiagnostic(diagnostic));
        }
    }

    private static string LeafFileName(string path)
    {
        var separator = path.LastIndexOfAny(PathSeparators);
        return separator >= 0 ? path.Substring(separator + 1) : path;
    }

    private static readonly char[] PathSeparators = { '/', '\\' };
}
