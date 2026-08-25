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
    private readonly string[] excludedDirectories;
    private readonly object sourceSnapshotSynchronization = new object();
    private Dictionary<string, SourceFileStamp> sourceSnapshot =
        new Dictionary<string, SourceFileStamp>(PathComparer);
    private WatchGraph watchGraph = WatchGraph.Empty;
    private int changeVersion;

    public CssHotReloadWorker(
        CssHotReloadOptions options,
        ProcessIdentity ownerIdentity)
    {
        this.options = options;
        this.ownerIdentity = ownerIdentity;
        eventLog = new CssHotReloadEventLog(options.EventLogPath);
        regenerator = new CssHotReloadRegenerator(options, eventLog);
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
        var processChangesTask = ProcessChangesAsync(linkedCancellation.Token);
        var monitorSourceSnapshotTask = MonitorSourceSnapshotAsync(
            linkedCancellation.Token);
        Task? monitorLifetimeTask = null;
        try
        {
            WriteStateFile();
            monitorLifetimeTask = MonitorLifetimeAsync(linkedCancellation.Token);
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

    private async Task ProcessChangesAsync(CancellationToken cancellationToken)
    {
        var processedVersion = Volatile.Read(ref changeVersion);
        while (true)
        {
            await Task.Delay(options.DebounceMilliseconds, cancellationToken);
            var observedVersion = Volatile.Read(ref changeVersion);
            if (observedVersion == processedVersion)
            {
                continue;
            }

            while (true)
            {
                await Task.Delay(options.DebounceMilliseconds, cancellationToken);
                var currentVersion = Volatile.Read(ref changeVersion);
                if (currentVersion == observedVersion)
                {
                    break;
                }

                observedVersion = currentVersion;
            }

            processedVersion = observedVersion;
            UpdateSourceSnapshot();
            await regenerator.RegenerateAsync(cancellationToken);
            // Give source writes racing the nested-build process boundary one bounded scheduling
            // window before reconciliation. Later changes remain covered by snapshot monitoring.
            await Task.Delay(
                Math.Min(50, options.DebounceMilliseconds),
                cancellationToken);
            QueueSourceSnapshotChange();
            eventLog.Append("settled");
        }
    }

    private async Task MonitorSourceSnapshotAsync(CancellationToken cancellationToken)
    {
        var interval = Math.Max(250, options.DebounceMilliseconds * 2);
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

    private async Task MonitorLifetimeAsync(CancellationToken cancellationToken)
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

        if (IsExcludedPath(fullPath))
        {
            return false;
        }

        lock (sourceSnapshotSynchronization)
        {
            if (watchGraph.Files.Contains(fullPath))
            {
                return true;
            }

            return watchGraph.Roots.Any(root => root.Matches(fullPath));
        }
    }

    private SourceCapture CaptureSourceSnapshot()
    {
        var graph = CreateWatchGraph();
        var snapshot = new Dictionary<string, SourceFileStamp>(PathComparer);
        foreach (var file in graph.Files)
        {
            TryAddFileStamp(snapshot, file);
        }

        foreach (var root in graph.Roots)
        {
            CaptureRootDirectory(snapshot, root);
        }

        return new SourceCapture(graph, snapshot);
    }

    private WatchGraph CreateWatchGraph()
    {
        var files = new HashSet<string>(PathComparer);
        var roots = new Dictionary<string, HashSet<string>>(PathComparer);
        foreach (var asset in options.GeneratedAssets)
        {
            foreach (var file in asset.WatchFiles)
            {
                files.Add(Path.GetFullPath(file));
            }

            foreach (var root in asset.WatchRoots)
            {
                AddWatchRoot(roots, root, asset.WatchExtensions);
            }

            if (string.IsNullOrEmpty(asset.DependencyManifestPath))
            {
                continue;
            }

            files.Add(asset.DependencyManifestPath);
            if (!GeneratedAssetDependencyManifest.TryRead(
                    asset.DependencyManifestPath,
                    out var manifest,
                    out var error))
            {
                eventLog.Append(
                    "manifest-error:" +
                    Path.GetFileName(asset.DependencyManifestPath) +
                    ":" + error);
                continue;
            }

            foreach (var file in manifest.Files)
            {
                files.Add(file);
            }

            foreach (var root in manifest.Roots)
            {
                AddWatchRoot(roots, root, asset.WatchExtensions);
            }
        }

        return new WatchGraph(
            files,
            roots.Select(entry => new WatchRoot(entry.Key, entry.Value)).ToArray());
    }

    private static void AddWatchRoot(
        IDictionary<string, HashSet<string>> roots,
        string path,
        IEnumerable<string> extensions)
    {
        var fullPath = Path.GetFullPath(path);
        if (!roots.TryGetValue(fullPath, out var existingExtensions))
        {
            existingExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            roots.Add(fullPath, existingExtensions);
        }

        existingExtensions.UnionWith(extensions);
    }

    private void CaptureRootDirectory(
        IDictionary<string, SourceFileStamp> snapshot,
        WatchRoot root)
    {
        TryAddDirectoryStamp(snapshot, root.Path);
        if (!Directory.Exists(root.Path))
        {
            return;
        }

        var pendingDirectories = new Stack<string>();
        var visitedDirectories = new HashSet<string>(PathComparer);
        pendingDirectories.Push(root.Path);
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
                if (root.AcceptsExtension(Path.GetExtension(file)))
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
            var currentCapture = CaptureSourceSnapshot();
            watchGraph = currentCapture.Graph;
            if (SnapshotsEqual(sourceSnapshot, currentCapture.Snapshot))
            {
                return false;
            }

            sourceSnapshot = currentCapture.Snapshot;
            return true;
        }
    }

    private void UpdateSourceSnapshot()
    {
        lock (sourceSnapshotSynchronization)
        {
            var capture = CaptureSourceSnapshot();
            watchGraph = capture.Graph;
            sourceSnapshot = capture.Snapshot;
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

    private bool IsExcludedDirectory(string path) =>
        IsExcludedPath(EnsureTrailingDirectorySeparator(path));

    private bool IsExcludedPath(string path) =>
        excludedDirectories.Any(
            excluded => path.StartsWith(excluded, PathComparison));

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
            snapshot[Path.GetFullPath(path)] = new SourceFileStamp(
                -1,
                information.Exists ? 1 : 0);
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

    private sealed class SourceCapture
    {
        public SourceCapture(
            WatchGraph graph,
            Dictionary<string, SourceFileStamp> snapshot)
        {
            Graph = graph;
            Snapshot = snapshot;
        }

        public WatchGraph Graph { get; }

        public Dictionary<string, SourceFileStamp> Snapshot { get; }
    }

    private sealed class WatchGraph
    {
        public static readonly WatchGraph Empty = new WatchGraph(
            new HashSet<string>(PathComparer),
            Array.Empty<WatchRoot>());

        public WatchGraph(HashSet<string> files, IReadOnlyList<WatchRoot> roots)
        {
            Files = files;
            Roots = roots;
        }

        public HashSet<string> Files { get; }

        public IReadOnlyList<WatchRoot> Roots { get; }
    }

    private sealed class WatchRoot
    {
        private readonly HashSet<string> extensions;
        private readonly string pathWithSeparator;

        public WatchRoot(string path, IEnumerable<string> extensions)
        {
            Path = System.IO.Path.GetFullPath(path);
            pathWithSeparator = EnsureTrailingDirectorySeparator(Path);
            this.extensions = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public string Path { get; }

        public bool Matches(string filePath) =>
            filePath.StartsWith(pathWithSeparator, PathComparison) &&
            AcceptsExtension(System.IO.Path.GetExtension(filePath));

        public bool AcceptsExtension(string extension) =>
            extensions.Contains(extension);
    }
}
