using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Orchestrates project-context resolution for one document ([V01.01.12.23], #259):
/// <see cref="ViuDocumentSupport.TryFindOwningViuProject"/> finds the owner,
/// <see cref="ViuProjectAssetsReader"/> resolves the restore artifacts, and the source cone is
/// read into an editor-neutral <see cref="LanguageProjectContext"/>. All file IO stays in the host.
/// Source text is cached per project
/// and re-validated by the assets reader's cheap-stat <see cref="ViuProjectAssets.CacheStamp"/> on
/// every call, which also implements recovery on the next request without a file watcher.
/// </summary>
internal sealed class ViuProjectContextReader
{
    // A cancellation-aware gate rather than a monitor lock: a request queued behind another
    // probe honors $/cancelRequest while waiting instead of only after acquisition. The coarse
    // gate still serializes concurrent probes so two semantic requests never duplicate the
    // identical disk scan, and every probe is bounded stat-and-read work.
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CachedProjectCone> conesByProjectFilePath =
        new(StringComparer.OrdinalIgnoreCase);

    internal (LanguageProjectContext? Context, ViuProjectContextStatus Status) Read(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) ||
            !uri.IsFile ||
            !ViuDocumentSupport.TryFindOwningViuProject(
                uri.LocalPath,
                out var projectFilePath) ||
            projectFilePath is null)
        {
            return (null, ViuProjectContextStatus.NoOwningProject);
        }

        var documentPath = Path.GetFullPath(uri.LocalPath);
        gate.Wait(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assets = ViuProjectAssetsReader.Read(projectFilePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (assets.Status.Kind is not (ViuProjectContextStatusKind.SemanticsActive or
                ViuProjectContextStatusKind.AssetsStale))
            {
                conesByProjectFilePath.Remove(projectFilePath);
                return (null, assets.Status);
            }

            if (!conesByProjectFilePath.TryGetValue(projectFilePath, out var cone) ||
                !string.Equals(cone.CacheStamp, assets.CacheStamp, StringComparison.Ordinal))
            {
                cone = new CachedProjectCone(
                    assets.CacheStamp,
                    ReadSourceDocuments(assets, cancellationToken));
                conesByProjectFilePath[projectFilePath] = cone;
            }

            // The open document is excluded from the disk set — its live editor text wins.
            var siblingDocuments = new List<LanguageProjectSourceDocument>(
                cone.SourceDocuments.Count);
            foreach (var sourceDocument in cone.SourceDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(
                        sourceDocument.FilePath,
                        documentPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    siblingDocuments.Add(sourceDocument);
                }
            }

            var context = new LanguageProjectContext(
                projectFilePath,
                assets.ProjectDirectory,
                assets.RootNamespace,
                assets.ReferenceAssemblyPaths,
                siblingDocuments,
                ViuProjectPreprocessorSymbols.Derive(
                    assets.TargetFramework,
                    assets.Configuration),
                assets.CacheStamp);
            return (context, assets.Status);
        }
        finally
        {
            gate.Release();
        }
    }

    private static IReadOnlyList<LanguageProjectSourceDocument> ReadSourceDocuments(
        ViuProjectAssets assets,
        CancellationToken cancellationToken)
    {
        var documents = new List<LanguageProjectSourceDocument>(
            assets.SourceFilePaths.Count + assets.ComponentFilePaths.Count);
        AppendSourceDocuments(
            assets.SourceFilePaths,
            isComponent: false,
            documents,
            cancellationToken);
        AppendSourceDocuments(
            assets.ComponentFilePaths,
            isComponent: true,
            documents,
            cancellationToken);
        return documents;
    }

    private static void AppendSourceDocuments(
        IReadOnlyList<string> filePaths,
        bool isComponent,
        List<LanguageProjectSourceDocument> documents,
        CancellationToken cancellationToken)
    {
        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                documents.Add(
                    new LanguageProjectSourceDocument(
                        filePath,
                        File.ReadAllText(filePath),
                        isComponent));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // A file deleted between enumeration and read degrades to one fewer sibling
                // document; the next request re-enumerates the cone.
            }
        }
    }

    private sealed record CachedProjectCone(
        string CacheStamp,
        IReadOnlyList<LanguageProjectSourceDocument> SourceDocuments);
}
