using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed class UtilityCssProjectContextProvider
{
    private const string ManifestFileName = "utilitycss.manifest.v1.json";

    private readonly Dictionary<string, UtilityCssProjectContextCacheEntry> cache =
        new(GetPathComparer());

    // Version 1 intentionally polls file modification times at request boundaries. It avoids a
    // watcher per project, remains deterministic in tests and remote workspaces, and refreshes the
    // manifest, catalog, entry stylesheet, and every resolved @reference before answering editor
    // data.
    internal UtilityCssProjectContext Get(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetDocumentPath(documentUri, out var documentPath) ||
            !TryFindProjectDirectory(documentPath, out var projectDirectory) ||
            !TryFindNewestManifest(projectDirectory, out var manifestPath))
        {
            return UtilityCssProjectContext.Default;
        }

        var manifestState = WatchedFileState.Read(manifestPath);
        if (cache.TryGetValue(projectDirectory, out var cached) &&
            cached.IsCurrent(manifestPath, manifestState, GetPathComparer()))
        {
            return cached.Context;
        }

        var dependencyStates = new List<WatchedFileState>();
        var context = LoadContext(
            manifestPath,
            dependencyStates,
            cancellationToken);
        cache[projectDirectory] = new UtilityCssProjectContextCacheEntry(
            manifestPath,
            manifestState,
            dependencyStates,
            context);
        return context;
    }

    private static UtilityCssProjectContext LoadContext(
        string manifestPath,
        ICollection<WatchedFileState> dependencyStates,
        CancellationToken cancellationToken)
    {
        var catalogPath = Path.Combine(
            Path.GetDirectoryName(manifestPath) ?? string.Empty,
            UtilityCssEditorCatalog.FileName);
        dependencyStates.Add(WatchedFileState.Read(catalogPath));
        var editorCatalog = UtilityCssEditorCatalog.Load(catalogPath);

        string? entryStylesheetPath;
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schemaVersion) ||
                !schemaVersion.TryGetInt32(out var version) ||
                version != 1 ||
                !root.TryGetProperty("entryStylesheetPath", out var entryPathElement))
            {
                return UtilityCssProjectContext.Default;
            }

            entryStylesheetPath = entryPathElement.ValueKind switch
            {
                JsonValueKind.String => entryPathElement.GetString(),
                JsonValueKind.Null => null,
                _ => null,
            };
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  ArgumentException or
                  NotSupportedException)
        {
            return UtilityCssProjectContext.Default;
        }

        if (string.IsNullOrWhiteSpace(entryStylesheetPath))
        {
            return CreateDefaultContext(editorCatalog);
        }

        string entryIdentity;
        try
        {
            entryIdentity = Path.GetFullPath(entryStylesheetPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return CreateDefaultContext(editorCatalog);
        }

        dependencyStates.Add(WatchedFileState.Read(entryIdentity));
        string entryCss;
        try
        {
            entryCss = File.ReadAllText(entryIdentity);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return CreateDefaultContext(editorCatalog);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sourceParse = UtilitySourceParser.Parse(
            entryCss,
            new UtilitySourceParseOptions
            {
                SourceIdentity = entryIdentity,
            },
            cancellationToken);
        var referenceGraph = BuildReferenceGraph(
            entryIdentity,
            entryCss,
            dependencyStates,
            cancellationToken);
        var theme = ParseTheme(
            entryIdentity,
            entryCss,
            sourceParse.Configuration,
            referenceGraph,
            cancellationToken);
        var compilationOptions = new UtilityProjectStylesheetCompilationOptions
        {
            SourceIdentity = entryIdentity,
            Registry = UtilityCssRegistry.BuiltIn,
            Theme = theme,
            ReferenceGraph = referenceGraph,
        };
        var definitionCompilation = UtilityProjectStylesheetCompiler.Compile(
            entryCss,
            Array.Empty<string>(),
            compilationOptions,
            cancellationToken);
        var variantRegistry = CreateVariantRegistry(
            theme,
            definitionCompilation.Variants);
        return new UtilityCssProjectContext(
            entryCss,
            compilationOptions,
            variantRegistry,
            editorCatalog);
    }

    private static UtilityCssProjectContext CreateDefaultContext(
        UtilityCssEditorCatalog editorCatalog) =>
        new(
            string.Empty,
            new UtilityProjectStylesheetCompilationOptions(),
            UtilityVariantRegistry.BuiltIn,
            editorCatalog);

    private static UtilityStylesheetReferenceGraph BuildReferenceGraph(
        string entryIdentity,
        string entryCss,
        ICollection<WatchedFileState> dependencyStates,
        CancellationToken cancellationToken)
        => UtilityStylesheetReferenceGraphBuilder.Build(
            entryIdentity,
            entryCss,
            (referencingSourceIdentity, specifier) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var referencingDirectory =
                    Path.GetDirectoryName(referencingSourceIdentity) ??
                    Path.GetDirectoryName(entryIdentity) ??
                    string.Empty;
                string resolvedIdentity;
                try
                {
                    resolvedIdentity = Path.GetFullPath(
                        Path.IsPathRooted(specifier)
                            ? specifier
                            : Path.Combine(referencingDirectory, specifier));
                }
                catch (Exception exception)
                    when (exception is ArgumentException or
                          NotSupportedException or
                          PathTooLongException)
                {
                    return null;
                }

                dependencyStates.Add(WatchedFileState.Read(resolvedIdentity));
                if (!File.Exists(resolvedIdentity))
                {
                    return null;
                }

                try
                {
                    return new UtilityStylesheetReference(
                        referencingSourceIdentity,
                        specifier,
                        resolvedIdentity,
                        File.ReadAllText(resolvedIdentity));
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    return null;
                }
            },
            cancellationToken);

    private static UtilityTheme ParseTheme(
        string entryIdentity,
        string entryCss,
        UtilitySourceConfiguration sourceConfiguration,
        UtilityStylesheetReferenceGraph referenceGraph,
        CancellationToken cancellationToken)
    {
        var theme = UtilityTheme.Default;
        foreach (var reference in GetThemeReferenceOrder(
                     entryIdentity,
                     referenceGraph))
        {
            cancellationToken.ThrowIfCancellationRequested();
            theme = UtilityThemeParser.Parse(
                    reference.Css,
                    new UtilityThemeParseOptions
                    {
                        BaseTheme = theme,
                        SourceIdentity = reference.ResolvedSourceIdentity,
                    },
                    cancellationToken)
                .Theme;
        }

        return UtilityThemeParser.Parse(
                entryCss,
                new UtilityThemeParseOptions
                {
                    BaseTheme = theme,
                    Prefix = sourceConfiguration.Prefix,
                    ImportedThemeOptions = sourceConfiguration.ImportedThemeOptions,
                    IsImportant = sourceConfiguration.IsImportant,
                    SourceIdentity = entryIdentity,
                },
                cancellationToken)
            .Theme;
    }

    private static IReadOnlyList<UtilityStylesheetReference> GetThemeReferenceOrder(
        string rootSourceIdentity,
        UtilityStylesheetReferenceGraph referenceGraph)
    {
        if (referenceGraph.References.Count == 0)
        {
            return Array.Empty<UtilityStylesheetReference>();
        }

        var comparer = GetPathComparer();
        var referencesBySource = referenceGraph.References
            .GroupBy(reference => reference.ReferencingSourceIdentity, comparer)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(reference => reference.Specifier, StringComparer.Ordinal)
                    .ToArray(),
                comparer);
        var visited = new HashSet<string>(comparer);
        var ordered = new List<UtilityStylesheetReference>();
        VisitThemeReferences(
            rootSourceIdentity,
            referencesBySource,
            visited,
            ordered);
        return ordered;
    }

    private static void VisitThemeReferences(
        string sourceIdentity,
        IReadOnlyDictionary<string, UtilityStylesheetReference[]> referencesBySource,
        ISet<string> visited,
        ICollection<UtilityStylesheetReference> ordered)
    {
        if (!visited.Add(sourceIdentity) ||
            !referencesBySource.TryGetValue(sourceIdentity, out var references))
        {
            return;
        }

        foreach (var reference in references)
        {
            if (visited.Contains(reference.ResolvedSourceIdentity))
            {
                continue;
            }

            VisitThemeReferences(
                reference.ResolvedSourceIdentity,
                referencesBySource,
                visited,
                ordered);
            ordered.Add(reference);
        }
    }

    private static UtilityVariantRegistry CreateVariantRegistry(
        UtilityTheme theme,
        IEnumerable<UtilityCustomVariantDefinition> customVariants)
    {
        var definitions = theme.VariantRegistry.Definitions.ToList();
        var names = new HashSet<string>(
            definitions.Select(definition => definition.Name),
            StringComparer.Ordinal);
        foreach (var customVariant in customVariants)
        {
            if (!names.Add(customVariant.Name))
            {
                continue;
            }

            definitions.Add(
                new UtilityVariantDefinition(
                    customVariant.Name,
                    UtilityVariantKind.Static,
                    UtilityVariantCategory.State));
        }

        return definitions.Count == theme.VariantRegistry.Definitions.Count
            ? theme.VariantRegistry
            : new UtilityVariantRegistry(definitions);
    }

    private static bool TryGetDocumentPath(
        string documentUri,
        out string documentPath)
    {
        documentPath = string.Empty;
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return false;
        }

        try
        {
            documentPath = Path.GetFullPath(uri.LocalPath);
            return true;
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryFindProjectDirectory(
        string documentPath,
        out string projectDirectory)
    {
        projectDirectory = string.Empty;
        var directory = Path.GetDirectoryName(documentPath);
        while (!string.IsNullOrEmpty(directory))
        {
            try
            {
                if (Directory.EnumerateFiles(
                        directory,
                        "*.csproj",
                        SearchOption.TopDirectoryOnly)
                    .Any())
                {
                    projectDirectory = directory;
                    return true;
                }
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    private static bool TryFindNewestManifest(
        string projectDirectory,
        out string manifestPath)
    {
        manifestPath = string.Empty;
        var objectDirectory = Path.Combine(projectDirectory, "obj");
        if (!Directory.Exists(objectDirectory))
        {
            return false;
        }

        try
        {
            var newest = Directory
                .EnumerateFiles(
                    objectDirectory,
                    ManifestFileName,
                    SearchOption.AllDirectories)
                .Where(path => string.Equals(
                    Path.GetFileName(Path.GetDirectoryName(path)),
                    "utilitycss",
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => new
                {
                    Path = Path.GetFullPath(path),
                    LastWriteTimeUtc = File.GetLastWriteTimeUtc(path),
                })
                .OrderByDescending(item => item.LastWriteTimeUtc)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (newest is null)
            {
                return false;
            }

            manifestPath = newest.Path;
            return true;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StringComparer GetPathComparer()
        => Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
