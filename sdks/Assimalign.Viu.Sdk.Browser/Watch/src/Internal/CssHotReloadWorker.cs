using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class CssHotReloadWorker
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly CssHotReloadOptions options;
    private readonly ProcessIdentity ownerIdentity;
    private readonly CssHotReloadRegenerator regenerator;
    private readonly CssHotReloadEventLog eventLog;
    private readonly HashSet<string> explicitWatchFiles;
    private readonly string[] excludedDirectories;
    private readonly object sourceSnapshotSynchronization = new object();
    private Dictionary<string, SourceFileStamp> sourceSnapshot =
        new Dictionary<string, SourceFileStamp>(PathComparer);
    private int changeVersion;

    public CssHotReloadWorker(
        CssHotReloadOptions options,
        ProcessIdentity ownerIdentity)
    {
        this.options = options;
        this.ownerIdentity = ownerIdentity;
        eventLog = new CssHotReloadEventLog(options.EventLogPath);
        regenerator = new CssHotReloadRegenerator(options, eventLog);
        explicitWatchFiles = options.ExplicitWatchFiles
            .Select(Path.GetFullPath)
            .ToHashSet(PathComparer);
        excludedDirectories = options.ExcludedDirectories
            .Select(EnsureTrailingDirectorySeparator)
            .Distinct(PathComparer)
            .ToArray();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        using var watcher = CreateWatcher();
        UpdateSourceSnapshot();
        watcher.EnableRaisingEvents = true;
        var processChangesTask = ProcessChangesAsync(
            linkedCancellation.Token);
        var monitorSourceSnapshotTask = MonitorSourceSnapshotAsync(
            linkedCancellation.Token);
        Task? monitorLifetimeTask = null;
        try
        {
            WriteStateFile();
            monitorLifetimeTask = MonitorLifetimeAsync(
                linkedCancellation.Token);
            await Task.WhenAny(
                processChangesTask,
                monitorSourceSnapshotTask,
                monitorLifetimeTask);
        }
        finally
        {
            await linkedCancellation.CancelAsync();
            await IgnoreCancellationAsync(processChangesTask);
            await IgnoreCancellationAsync(monitorSourceSnapshotTask);
            if (monitorLifetimeTask is not null)
            {
                await IgnoreCancellationAsync(monitorLifetimeTask);
            }

            watcher.EnableRaisingEvents = false;
            DeleteOwnedStateFile();
        }
    }

    private FileSystemWatcher CreateWatcher()
    {
        var watcher = new FileSystemWatcher(options.ProjectDirectory)
        {
            IncludeSubdirectories = true,
            Filter = "*",
            InternalBufferSize = 16 * 1024,
            NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.DirectoryName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.CreationTime,
        };
        watcher.Changed += (_, eventArguments) =>
            QueueIfRelevant(eventArguments.FullPath);
        watcher.Created += (_, eventArguments) =>
            QueueIfRelevant(eventArguments.FullPath);
        watcher.Deleted += (_, eventArguments) =>
            QueueIfRelevant(eventArguments.FullPath);
        watcher.Renamed += (_, eventArguments) =>
        {
            QueueIfRelevant(eventArguments.OldFullPath);
            QueueIfRelevant(eventArguments.FullPath);
        };
        watcher.Error += (_, eventArguments) =>
        {
            eventLog.Append(
                "watcher-error:" +
                eventArguments.GetException().GetType().Name);
            QueueChange();
        };
        return watcher;
    }

    private async Task ProcessChangesAsync(
        CancellationToken cancellationToken)
    {
        var processedVersion = Volatile.Read(ref changeVersion);
        while (true)
        {
            await Task.Delay(
                options.DebounceMilliseconds,
                cancellationToken);
            var observedVersion = Volatile.Read(ref changeVersion);
            if (observedVersion == processedVersion)
            {
                continue;
            }

            while (true)
            {
                await Task.Delay(
                    options.DebounceMilliseconds,
                    cancellationToken);
                var currentVersion = Volatile.Read(ref changeVersion);
                if (currentVersion == observedVersion)
                {
                    break;
                }

                observedVersion = currentVersion;
            }

            processedVersion = observedVersion;
            await regenerator.RegenerateAsync(cancellationToken);
            QueueSourceSnapshotChange();
        }
    }

    private async Task MonitorSourceSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var interval = Math.Max(
            250,
            options.DebounceMilliseconds * 2);
        while (true)
        {
            await Task.Delay(interval, cancellationToken);
            try
            {
                QueueSourceSnapshotChange();
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                eventLog.Append(
                    "snapshot-error:" +
                    exception.GetType().Name);
            }
        }
    }

    private async Task MonitorLifetimeAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(250, cancellationToken);
            if (!File.Exists(options.StateFilePath) ||
                !ProcessLifetime.IsAlive(ownerIdentity))
            {
                eventLog.Append(
                    !File.Exists(options.StateFilePath)
                        ? "stop:state"
                        : "stop:owner");
                return;
            }
        }
    }

    private void QueueIfRelevant(string path)
    {
        if (IsRelevant(path))
        {
            eventLog.Append("change:" + Path.GetFileName(path));
            QueueChange();
        }
    }

    private bool IsRelevant(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException)
        {
            return false;
        }

        if (excludedDirectories.Any(
                directory => fullPath.StartsWith(directory, PathComparison)))
        {
            return false;
        }

        if (explicitWatchFiles.Contains(fullPath))
        {
            return true;
        }

        return options.WatchComponents && IsComponentFile(fullPath);
    }

    private Dictionary<string, SourceFileStamp> CaptureSourceSnapshot()
    {
        var snapshot = new Dictionary<string, SourceFileStamp>(PathComparer);
        if (options.WatchComponents)
        {
            CaptureComponentDirectory(
                snapshot,
                options.ProjectDirectory);
        }

        foreach (var file in explicitWatchFiles)
        {
            TryAddFileStamp(snapshot, file);
        }

        return snapshot;
    }

    private void CaptureComponentDirectory(
        IDictionary<string, SourceFileStamp> snapshot,
        string rootDirectory)
    {
        TryAddDirectoryStamp(snapshot, rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        var pendingDirectories = new Stack<string>();
        var visitedDirectories = new HashSet<string>(PathComparer);
        pendingDirectories.Push(rootDirectory);
        while (pendingDirectories.Count > 0)
        {
            var directory = Path.GetFullPath(pendingDirectories.Pop());
            if (!visitedDirectories.Add(directory) ||
                IsExcludedDirectory(directory))
            {
                continue;
            }

            string[] childDirectories;
            string[] files;
            try
            {
                childDirectories = Directory.GetDirectories(directory);
                files = Directory.GetFiles(directory);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (!IsExcludedDirectory(childDirectory) &&
                    !IsReparsePoint(childDirectory))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }

            foreach (var file in files)
            {
                if (IsComponentFile(file))
                {
                    TryAddFileStamp(snapshot, file);
                }
            }
        }
    }

    private bool RefreshSourceSnapshot()
    {
        lock (sourceSnapshotSynchronization)
        {
            var currentSnapshot = CaptureSourceSnapshot();
            if (SnapshotsEqual(sourceSnapshot, currentSnapshot))
            {
                return false;
            }

            sourceSnapshot = currentSnapshot;
            return true;
        }
    }

    private void UpdateSourceSnapshot()
    {
        lock (sourceSnapshotSynchronization)
        {
            sourceSnapshot = CaptureSourceSnapshot();
        }
    }

    private void QueueSourceSnapshotChange()
    {
        if (!RefreshSourceSnapshot())
        {
            return;
        }

        QueueChange();
        eventLog.Append("snapshot-change");
    }

    private static bool IsComponentFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".viu", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vue", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsExcludedDirectory(string path)
    {
        var directory = EnsureTrailingDirectorySeparator(path);
        return excludedDirectories.Any(
            excluded => directory.StartsWith(excluded, PathComparison));
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void TryAddFileStamp(
        IDictionary<string, SourceFileStamp> snapshot,
        string path)
    {
        try
        {
            var information = new FileInfo(path);
            if (information.Exists)
            {
                snapshot[Path.GetFullPath(path)] = new SourceFileStamp(
                    information.Length,
                    information.LastWriteTimeUtc.Ticks);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A concurrent editor replacement is observed on the next snapshot interval.
        }
    }

    private static void TryAddDirectoryStamp(
        IDictionary<string, SourceFileStamp> snapshot,
        string path)
    {
        try
        {
            var information = new DirectoryInfo(path);
            if (information.Exists)
            {
                snapshot[Path.GetFullPath(path)] = new SourceFileStamp(
                    0,
                    information.LastWriteTimeUtc.Ticks);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A concurrently created source root is observed on the next snapshot interval.
        }
    }

    private static bool SnapshotsEqual(
        IReadOnlyDictionary<string, SourceFileStamp> first,
        IReadOnlyDictionary<string, SourceFileStamp> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (var entry in first)
        {
            if (!second.TryGetValue(entry.Key, out var value) ||
                !entry.Value.Equals(value))
            {
                return false;
            }
        }

        return true;
    }

    private void QueueChange()
    {
        Interlocked.Increment(ref changeVersion);
    }

    private void WriteStateFile()
    {
        var directory = Path.GetDirectoryName(options.StateFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var currentProcess = Process.GetCurrentProcess();
        var text =
            "worker=" +
            currentProcess.Id +
            Environment.NewLine +
            "worker-start=" +
            currentProcess.StartTime.ToUniversalTime().Ticks +
            Environment.NewLine +
            "owner=" +
            ownerIdentity.ProcessIdentifier +
            Environment.NewLine;
        File.WriteAllText(
            options.StateFilePath,
            text,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void DeleteOwnedStateFile()
    {
        try
        {
            if (!File.Exists(options.StateFilePath))
            {
                return;
            }

            var firstLine = File.ReadLines(options.StateFilePath).FirstOrDefault();
            if (string.Equals(
                    firstLine,
                    "worker=" + Environment.ProcessId,
                    StringComparison.Ordinal))
            {
                File.Delete(options.StateFilePath);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup. A later launcher validates the recorded process before reuse.
        }
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected when the owner exits, the state file is removed, or dotnet watch is cancelled.
        }
    }

    private readonly record struct SourceFileStamp(
        long Length,
        long LastWriteTimeUtcTicks);

}
