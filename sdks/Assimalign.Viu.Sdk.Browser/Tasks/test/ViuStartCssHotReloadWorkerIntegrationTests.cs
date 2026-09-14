using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

using Assimalign.Viu.Sdk.Browser.Tasks;

namespace Assimalign.Viu.Sdk.Browser.Tasks.Tests;

public sealed class ViuStartCssHotReloadWorkerIntegrationTests
{
    // [V01.01.12.05.03], #370: real task-to-worker startup must recognize the child's
    // cross-process identity, reuse it, and allow it to exit after the state file is removed.
    [Fact]
    public void Execute_RealWorker_StartsReusesAndShutsDownWhenStateIsRemoved()
    {
        var repositoryDirectory = FindRepositoryDirectory();
        var scratchRoot = Path.Combine(repositoryDirectory, "_out", "issue370-tests");
        var directory = Path.Combine(scratchRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var stateFilePath = Path.Combine(directory, "obj", "worker.state");
        Process? worker = null;
        try
        {
            var projectPath = Path.Combine(directory, "Probe.proj");
            var sourcePath = Path.Combine(directory, "Probe.vue");
            File.WriteAllText(projectPath, "<Project><Target Name=\"GenerateProbeAsset\" /></Project>");
            File.WriteAllText(sourcePath, "<template><div /></template>");
            var asset = new TaskItem(Path.Combine(directory, "wwwroot", "probe.css"));
            asset.SetMetadata("WatchFiles", sourcePath);
            asset.SetMetadata("RegenerationTarget", "GenerateProbeAsset");
            asset.SetMetadata("StaticWebAssetPath", "wwwroot/probe.css");
            asset.SetMetadata("RemovalBehavior", "PreserveEmpty");
            var workerAssemblyPath = Directory.GetFiles(
                    Path.Combine(repositoryDirectory, "_out", "dotnet", "sdk"),
                    "Assimalign.Viu.Sdk.CssHotReload.dll",
                    SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .First();
            var buildEngine = new RecordingBuildEngine();

            ViuStartCssHotReloadWorker CreateTask() => new ViuStartCssHotReloadWorker
            {
                BuildEngine = buildEngine,
                WorkerAssemblyPath = workerAssemblyPath,
                ProjectPath = projectPath,
                ProjectDirectory = directory,
                StateFilePath = stateFilePath,
                EventLogPath = stateFilePath + ".events",
                DotNetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                GeneratedAssets = new ITaskItem[] { asset },
                ExcludedDirectories = new ITaskItem[]
                {
                    new TaskItem(Path.Combine(directory, "obj")),
                },
            };

            CreateTask().Execute().ShouldBeTrue(string.Join(Environment.NewLine, buildEngine.Errors));
            CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out var workerIdentifier)
                .ShouldBeTrue();
            worker = Process.GetProcessById(workerIdentifier);
            buildEngine.Messages.ShouldContain(message => message.Contains(" is watching ", StringComparison.Ordinal));
            File.Exists(stateFilePath + ".stdout.log").ShouldBeTrue();
            File.Exists(stateFilePath + ".stderr.log").ShouldBeTrue();

            CreateTask().Execute().ShouldBeTrue(string.Join(Environment.NewLine, buildEngine.Errors));
            CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out var reusedWorkerIdentifier)
                .ShouldBeTrue();
            reusedWorkerIdentifier.ShouldBe(workerIdentifier);
            buildEngine.Messages.ShouldContain(message => message.Contains(" is already active ", StringComparison.Ordinal));
            buildEngine.Errors.ShouldBeEmpty();
            buildEngine.Warnings.ShouldBeEmpty();

            File.Delete(stateFilePath);
            worker.WaitForExit(5000).ShouldBeTrue("The worker must stop after its state file is removed.");
            File.ReadAllText(stateFilePath + ".events").ShouldContain("stop:state");
        }
        finally
        {
            if (File.Exists(stateFilePath))
            {
                File.Delete(stateFilePath);
            }

            if (worker is not null)
            {
                try
                {
                    if (!worker.HasExited && !worker.WaitForExit(5000))
                    {
                        worker.Kill(entireProcessTree: true);
                        worker.WaitForExit(5000);
                    }
                }
                finally
                {
                    worker.Dispose();
                }
            }

            var resolvedDirectory = Path.GetFullPath(directory);
            resolvedDirectory.StartsWith(
                Path.GetFullPath(scratchRoot) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal).ShouldBeTrue();
            Directory.Delete(resolvedDirectory, recursive: true);
        }
    }

    private static string FindRepositoryDirectory()
    {
        for (var candidate = new DirectoryInfo(AppContext.BaseDirectory);
            candidate is not null;
            candidate = candidate.Parent)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "Assimalign.Viu.slnx")))
            {
                return candidate.FullName;
            }
        }

        throw new InvalidOperationException("The test output must remain beneath the Viu repository.");
    }

    private sealed class RecordingBuildEngine : IBuildEngine
    {
        public List<string> Messages { get; } = new List<string>();

        public List<string> Errors { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

        public void LogErrorEvent(BuildErrorEventArgs eventArguments) =>
            Errors.Add(eventArguments.Message ?? string.Empty);

        public void LogWarningEvent(BuildWarningEventArgs eventArguments) =>
            Warnings.Add(eventArguments.Message ?? string.Empty);

        public void LogMessageEvent(BuildMessageEventArgs eventArguments) =>
            Messages.Add(eventArguments.Message ?? string.Empty);

        public void LogCustomEvent(CustomBuildEventArgs eventArguments) =>
            Messages.Add(eventArguments.Message ?? string.Empty);

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs) =>
            throw new NotSupportedException("Worker startup does not request an in-process build.");
    }
}
