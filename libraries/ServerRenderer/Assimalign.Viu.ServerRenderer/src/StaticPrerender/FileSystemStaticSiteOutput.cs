using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Writes complete UTF-8 documents beneath one filesystem directory.</summary>
/// <remarks>Each file is replaced only after a sibling temporary file is complete. Reparse-point paths are rejected. The output directory must not be changed concurrently. Specified by <c>[SSG-2]</c> and <c>[SSG-5]</c>.</remarks>
public sealed class FileSystemStaticSiteOutput : IStaticSiteOutput
{
    private readonly string _directory;

    /// <summary>Sets the destination root without creating or modifying any file.</summary>
    /// <param name="directory">The output directory, resolved to an absolute path once.</param>
    public FileSystemStaticSiteOutput(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <inheritdoc/>
    public async ValueTask<string> WriteAsync(string relativePath, string document, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (string segment in relativePath.Split('/'))
        {
            StaticSitePath.ValidateSegment(segment);
        }

        string destination = Path.GetFullPath(Path.Combine(_directory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = Path.EndsInDirectorySeparator(_directory)
            ? _directory
            : _directory + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The static document must remain beneath the output directory.", nameof(relativePath));
        }

        RejectReparsePoints(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, document, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            // Publish already compressed the original host page. Never serve those stale bytes.
            File.Delete(destination + ".gz");
            File.Delete(destination + ".br");
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return destination;
    }

    private static void RejectReparsePoints(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Static output cannot traverse reparse point '{current}'.");
            }
        }
    }
}
