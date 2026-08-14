using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Discovers build-contributed class catalogs for one document and turns their file contents into
/// an immutable language-service configuration. Discovery and file IO remain host concerns
/// ([V01.01.12.30], #346).
/// </summary>
internal sealed class ViuClassCatalogReader
{
    private const string CatalogSearchPattern = "*.classcatalog.v1.json";

    // A cancellation-aware gate keeps concurrent feature requests from duplicating the same obj
    // walk while still allowing a queued request to honor $/cancelRequest.
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CachedClassCatalogs> catalogsByProjectDirectory =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the selected catalog files for the document's closest project-containing directory. The same
    /// configuration instance is returned while every selected path, modification time, and length
    /// remains unchanged.
    /// </summary>
    internal LanguageClassCatalogConfiguration? Read(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        if (!TryFindOwningProjectDirectory(documentUri, out var projectDirectory) ||
            projectDirectory is null)
        {
            return null;
        }

        gate.Wait(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            catalogsByProjectDirectory.TryGetValue(projectDirectory, out var cachedCatalogs);

            if (!TrySelectCatalogFiles(
                    projectDirectory,
                    cancellationToken,
                    out var selectedFiles))
            {
                // The failed discovery cannot prove that any cached file is still readable, so
                // omit catalogs for this request. Keep the cache only as a future identity baseline;
                // the next request always probes again and can recover without a restart.
                return null;
            }

            if (selectedFiles.Count == 0)
            {
                catalogsByProjectDirectory.Remove(projectDirectory);
                return null;
            }

            if (cachedCatalogs is not null &&
                HaveSameState(cachedCatalogs.SelectedFiles, selectedFiles))
            {
                return cachedCatalogs.Configuration;
            }

            var catalogJsonDocuments = new List<string>(selectedFiles.Count);
            var hadReadFailure = false;
            foreach (var selectedFile in selectedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    catalogJsonDocuments.Add(File.ReadAllText(selectedFile.FilePath));
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    // Omit only the unreadable file for this request. Do not cache the partial
                    // result, so a transient failure is retried at the next feature boundary.
                    hadReadFailure = true;
                }
            }

            if (hadReadFailure)
            {
                return catalogJsonDocuments.Count == 0
                    ? null
                    : new LanguageClassCatalogConfiguration(catalogJsonDocuments);
            }

            var configuration = new LanguageClassCatalogConfiguration(catalogJsonDocuments);
            catalogsByProjectDirectory[projectDirectory] = new CachedClassCatalogs(
                selectedFiles,
                configuration);
            return configuration;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool TryFindOwningProjectDirectory(
        string documentUri,
        out string? projectDirectory)
    {
        projectDirectory = null;
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return false;
        }

        string documentPath;
        try
        {
            documentPath = Path.GetFullPath(uri.LocalPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(documentPath)!);
        while (directory is not null)
        {
            if (directory.Exists)
            {
                string[] projectFilePaths;
                try
                {
                    projectFilePaths = Directory.GetFiles(
                        directory.FullName,
                        "*.csproj",
                        SearchOption.TopDirectoryOnly);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    return false;
                }

                if (projectFilePaths.Length != 0)
                {
                    projectDirectory = directory.FullName;
                    return true;
                }
            }

            directory = directory.Parent;
        }

        return false;
    }

    private static bool TrySelectCatalogFiles(
        string projectDirectory,
        CancellationToken cancellationToken,
        out IReadOnlyList<ClassCatalogFileState> selectedFiles)
    {
        var objectDirectory = Path.Combine(projectDirectory, "obj");
        if (!Directory.Exists(objectDirectory))
        {
            selectedFiles = Array.Empty<ClassCatalogFileState>();
            return true;
        }

        var newestByFileName = new Dictionary<string, ClassCatalogFileState>(
            StringComparer.Ordinal);
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(
                         objectDirectory,
                         CatalogSearchPattern,
                         SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(filePath);
                var fileInfo = new FileInfo(fullPath);
                var candidate = new ClassCatalogFileState(
                    fileInfo.Name,
                    fullPath,
                    fileInfo.LastWriteTimeUtc.Ticks,
                    fileInfo.Length);

                if (!newestByFileName.TryGetValue(candidate.FileName, out var current) ||
                    IsPreferred(candidate, current))
                {
                    newestByFileName[candidate.FileName] = candidate;
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            selectedFiles = Array.Empty<ClassCatalogFileState>();
            return false;
        }

        var orderedFileNames = new List<string>(newestByFileName.Keys);
        orderedFileNames.Sort(StringComparer.Ordinal);
        var results = new List<ClassCatalogFileState>(orderedFileNames.Count);
        foreach (var fileName in orderedFileNames)
        {
            results.Add(newestByFileName[fileName]);
        }

        selectedFiles = results;
        return true;
    }

    private static bool IsPreferred(
        ClassCatalogFileState candidate,
        ClassCatalogFileState current)
        => candidate.LastWriteTimeUtcTicks > current.LastWriteTimeUtcTicks ||
            candidate.LastWriteTimeUtcTicks == current.LastWriteTimeUtcTicks &&
            string.Compare(candidate.FilePath, current.FilePath, StringComparison.Ordinal) < 0;

    private static bool HaveSameState(
        IReadOnlyList<ClassCatalogFileState> previous,
        IReadOnlyList<ClassCatalogFileState> current)
    {
        if (previous.Count != current.Count)
        {
            return false;
        }

        for (var index = 0; index < previous.Count; index++)
        {
            if (!previous[index].Equals(current[index]))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record CachedClassCatalogs(
        IReadOnlyList<ClassCatalogFileState> SelectedFiles,
        LanguageClassCatalogConfiguration Configuration);

    private sealed record ClassCatalogFileState(
        string FileName,
        string FilePath,
        long LastWriteTimeUtcTicks,
        long Length);
}
