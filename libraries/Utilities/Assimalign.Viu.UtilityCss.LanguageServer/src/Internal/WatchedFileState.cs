using System;
using System.IO;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal readonly record struct WatchedFileState(
    string Path,
    bool Exists,
    long LastWriteTimeUtcTicks,
    long Length)
{
    internal static WatchedFileState Read(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists
                ? new WatchedFileState(
                    file.FullName,
                    true,
                    file.LastWriteTimeUtc.Ticks,
                    file.Length)
                : new WatchedFileState(
                    System.IO.Path.GetFullPath(path),
                    false,
                    0,
                    0);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  ArgumentException or
                  NotSupportedException)
        {
            return new WatchedFileState(path, false, 0, 0);
        }
    }

    internal bool IsCurrent() => Equals(Read(Path));
}
