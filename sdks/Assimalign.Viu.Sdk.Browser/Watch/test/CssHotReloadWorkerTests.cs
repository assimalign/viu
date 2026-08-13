using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Shouldly;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Assimalign.Viu.Sdk.CssHotReload.Tests;

public sealed class CssHotReloadWorkerTests
{
    [Fact]
    public void WatchCollection_DebugDotNetWatch_RegistersComponentStylesheetAsStaticFile()
    {
        var context = TestContext.Create();
        try
        {
            File.Exists(context.BundlePath).ShouldBeFalse();
            var debugResultPath = Path.Combine(context.DirectoryPath, "debug-watch.txt");
            RunMsBuild(
                context,
                "ProbeWatch",
                new Dictionary<string, string>
                {
                    ["DotNetWatchBuild"] = "true",
                    ["DesignTimeBuild"] = "true",
                    ["ViuCssHotReloadLaunchWorker"] = "false",
                    ["ProbeOutput"] = debugResultPath,
                });

            var debugLines = File.ReadAllLines(debugResultPath);
            debugLines.ShouldContain(
                line => line.EndsWith(
                    "Probe.viu.css|wwwroot/Probe.viu.css",
                    StringComparison.OrdinalIgnoreCase));
            debugLines.Count(
                    line => line.Contains(".viu.css|", StringComparison.OrdinalIgnoreCase))
                .ShouldBe(1);

            var releaseResultPath = Path.Combine(context.DirectoryPath, "release-watch.txt");
            RunMsBuild(
                context,
                "ProbeWatch",
                new Dictionary<string, string>
                {
                    ["Configuration"] = "Release",
                    ["DotNetWatchBuild"] = "true",
                    ["DesignTimeBuild"] = "true",
                    ["ViuCssHotReloadLaunchWorker"] = "false",
                    ["ProbeOutput"] = releaseResultPath,
                });
            ReadExistingLines(releaseResultPath).ShouldBeEmpty();

            var ordinaryBuildResultPath = Path.Combine(context.DirectoryPath, "ordinary-watch.txt");
            RunMsBuild(
                context,
                "ProbeWatch",
                new Dictionary<string, string>
                {
                    ["DotNetWatchBuild"] = "false",
                    ["DesignTimeBuild"] = "false",
                    ["ViuCssHotReloadLaunchWorker"] = "false",
                    ["ProbeOutput"] = ordinaryBuildResultPath,
                });
            ReadExistingLines(ordinaryBuildResultPath).ShouldBeEmpty();
        }
        finally
        {
            context.Dispose();
        }
    }

    [Fact]
    public void Worker_ComponentChanges_RegeneratesBundleAndIgnoresHostMarkup()
    {
        var context = TestContext.Create();
        Process? workerProcess = null;
        try
        {
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            File.ReadAllText(context.BundlePath).ShouldContain("color: red");

            workerProcess = StartWorker(context);
            WaitFor(() => File.Exists(context.StatePath), "worker state file");

            WriteWatchedFile(
                context.ComponentPath,
                "<template><div /></template><style>.component { color: blue; }</style>");
            WaitForEventCount(context.EventLogPath, 1);
            WaitFor(
                () => File.ReadAllText(context.BundlePath)
                    .Contains("color: blue", StringComparison.Ordinal),
                "component stylesheet regeneration");

            WriteWatchedFile(
                context.IndexPath,
                "<div class=\"unrelated\"></div>");
            Thread.Sleep(500);
            CountCompletedEvents(context.EventLogPath).ShouldBe(1);
        }
        finally
        {
            StopWorker(workerProcess);
            context.Dispose();
        }
    }

    [Fact]
    public void Worker_NoOpRemovalAndShutdown_PreservesComponentCssIncrementalContract()
    {
        var context = TestContext.Create();
        Process? workerProcess = null;
        try
        {
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });

            var originalCss = File.ReadAllText(context.BundlePath);
            var originalWriteTime = File.GetLastWriteTimeUtc(context.BundlePath);

            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            File.ReadAllText(context.BundlePath).ShouldBe(originalCss);
            File.GetLastWriteTimeUtc(context.BundlePath).ShouldBe(originalWriteTime);

            File.WriteAllText(
                context.ComponentPath,
                "<template><div /></template>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            new FileInfo(context.BundlePath).Length.ShouldBe(0);
            File.Exists(context.BundlePath + ".hot-reload-empty").ShouldBeTrue();

            workerProcess = StartWorker(context);
            WaitFor(() => File.Exists(context.StatePath), "worker state file");
            workerProcess.Id.ShouldBe(ReadWorkerProcessIdentifier(context.StatePath));
            using var duplicateWorker = StartWorker(context);
            duplicateWorker.WaitForExit(5000).ShouldBeTrue();
            duplicateWorker.ExitCode.ShouldBe(0);
            ReadWorkerProcessIdentifier(context.StatePath).ShouldBe(workerProcess.Id);

            File.Delete(context.StatePath);
            WaitFor(
                () =>
                {
                    workerProcess.Refresh();
                    return workerProcess.HasExited;
                },
                "worker shutdown after state-file removal");

            RunMsBuild(context, "_ViuBundleSingleFileComponentCss", new Dictionary<string, string>());
            File.Exists(context.BundlePath).ShouldBeFalse();
            File.Exists(context.BundlePath + ".hot-reload-empty").ShouldBeFalse();
        }
        finally
        {
            StopWorker(workerProcess);
            context.Dispose();
        }
    }

    private static void StopWorker(Process? workerProcess)
    {
        if (workerProcess is null)
        {
            return;
        }

        try
        {
            workerProcess.Refresh();
            if (!workerProcess.HasExited)
            {
                workerProcess.Kill(entireProcessTree: true);
                workerProcess.WaitForExit(5000);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            // The worker already exited between the status check and cleanup.
        }

        workerProcess.Dispose();
    }

    private static void WriteWatchedFile(string path, string content)
    {
        var temporaryPath = path + ".save";
        File.WriteAllText(
            temporaryPath,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static Process StartWorker(TestContext context)
    {
        using var currentProcess = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = GetDotNetHostPath(),
            WorkingDirectory = context.DirectoryPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        startInfo.ArgumentList.Add(context.WorkerAssemblyPath);
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(context.ProjectPath);
        startInfo.ArgumentList.Add("--project-directory");
        startInfo.ArgumentList.Add(context.DirectoryPath);
        startInfo.ArgumentList.Add("--state-file");
        startInfo.ArgumentList.Add(context.StatePath);
        startInfo.ArgumentList.Add("--event-log");
        startInfo.ArgumentList.Add(context.EventLogPath);
        startInfo.ArgumentList.Add("--owner-process-id");
        startInfo.ArgumentList.Add(
            currentProcess.Id.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--watch-components");
        startInfo.ArgumentList.Add("--exclude-directory");
        startInfo.ArgumentList.Add(Path.Combine(context.DirectoryPath, "obj"));
        startInfo.ArgumentList.Add("--exclude-directory");
        startInfo.ArgumentList.Add(Path.Combine(context.DirectoryPath, "bin"));
        return Process.Start(startInfo) ??
            throw new InvalidOperationException("The CSS Hot Reload worker could not be started.");
    }

    private static void RunMsBuild(
        TestContext context,
        string target,
        IReadOnlyDictionary<string, string> properties)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetDotNetHostPath(),
            WorkingDirectory = context.DirectoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(context.ProjectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-verbosity:quiet");
        startInfo.ArgumentList.Add("-target:" + target);
        foreach (var property in properties)
        {
            startInfo.ArgumentList.Add(
                "-property:" + property.Key + "=" + property.Value);
        }

        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("MSBuild did not finish within thirty seconds.");
        }

        process.ExitCode.ShouldBe(
            0,
            "MSBuild failed." +
            Environment.NewLine +
            standardOutput +
            Environment.NewLine +
            standardError);
    }

    private static void WaitForEventCount(string eventLogPath, int count)
    {
        try
        {
            WaitFor(
                () => CountCompletedEvents(eventLogPath) >= count,
                count.ToString(CultureInfo.InvariantCulture) + " completed regenerations");
        }
        catch (TimeoutException exception)
        {
            var events = File.Exists(eventLogPath)
                ? string.Join(", ", File.ReadAllLines(eventLogPath))
                : "<no event log>";
            throw new TimeoutException(
                exception.Message + " Observed events: " + events,
                exception);
        }
    }

    private static int CountCompletedEvents(string eventLogPath)
    {
        try
        {
            return !File.Exists(eventLogPath)
                ? 0
                : File.ReadAllLines(eventLogPath)
                    .Count(line => line.StartsWith("complete:", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static void WaitFor(Func<bool> predicate, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            Thread.Sleep(25);
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }

    private static int ReadWorkerProcessIdentifier(string statePath)
    {
        var line = File.ReadAllLines(statePath)
            .Single(value => value.StartsWith("worker=", StringComparison.Ordinal));
        return int.Parse(
            line.Substring("worker=".Length),
            NumberStyles.None,
            CultureInfo.InvariantCulture);
    }

    private static string[] ReadExistingLines(string path) =>
        File.Exists(path)
            ? File.ReadAllLines(path)
            : Array.Empty<string>();

    private static string GetDotNetHostPath() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private sealed class TestContext : IDisposable
    {
        private TestContext(
            string directoryPath,
            string projectPath,
            string indexPath,
            string componentPath,
            string bundlePath,
            string statePath,
            string eventLogPath,
            string workerAssemblyPath)
        {
            DirectoryPath = directoryPath;
            ProjectPath = projectPath;
            IndexPath = indexPath;
            ComponentPath = componentPath;
            BundlePath = bundlePath;
            StatePath = statePath;
            EventLogPath = eventLogPath;
            WorkerAssemblyPath = workerAssemblyPath;
        }

        public string DirectoryPath { get; }

        public string ProjectPath { get; }

        public string IndexPath { get; }

        public string ComponentPath { get; }

        public string BundlePath { get; }

        public string StatePath { get; }

        public string EventLogPath { get; }

        public string WorkerAssemblyPath { get; }

        public static TestContext Create()
        {
            var repositoryDirectory = FindRepositoryDirectory();
            var bundleTaskAssemblyPath = FindOutputFile(
                repositoryDirectory,
                "Assimalign.Viu.Sdk.Tasks.dll");
            var browserTaskAssemblyPath = FindOutputFile(
                repositoryDirectory,
                "Assimalign.Viu.Sdk.Browser.Tasks.dll");
            var workerAssemblyPath = FindOutputFile(
                repositoryDirectory,
                "Assimalign.Viu.Sdk.CssHotReload.dll");
            var directoryPath = Path.Combine(
                Path.GetTempPath(),
                "viu-css-hot-reload-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var projectPath = Path.Combine(directoryPath, "Probe.proj");
            var indexPath = Path.Combine(directoryPath, "Index.html");
            var componentPath = Path.Combine(directoryPath, "App.vue");
            File.WriteAllText(indexPath, "<div></div>");
            File.WriteAllText(
                componentPath,
                "<template><div /></template><style>.component { color: red; }</style>");

            var statePath = Path.Combine(directoryPath, "obj", "worker.state");
            var eventLogPath = Path.Combine(directoryPath, "obj", "worker.events");
            var componentTargetsPath = Path.Combine(
                repositoryDirectory,
                "build",
                "Targets",
                "Build.Css.Bundling.targets");
            var hotReloadTargetsPath = Path.Combine(
                repositoryDirectory,
                "build",
                "Targets",
                "Build.Css.HotReload.targets");
            var projectText =
                "<Project>" + Environment.NewLine +
                "  <PropertyGroup>" + Environment.NewLine +
                "    <Configuration>Debug</Configuration>" + Environment.NewLine +
                "    <PackageId>Probe</PackageId>" + Environment.NewLine +
                "    <BaseIntermediateOutputPath>obj\\</BaseIntermediateOutputPath>" + Environment.NewLine +
                "    <IntermediateOutputPath>obj\\</IntermediateOutputPath>" + Environment.NewLine +
                "    <BaseOutputPath>bin\\</BaseOutputPath>" + Environment.NewLine +
                "    <ViuUseSingleFileComponents>true</ViuUseSingleFileComponents>" + Environment.NewLine +
                "    <ViuBundleSingleFileComponentCss>true</ViuBundleSingleFileComponentCss>" + Environment.NewLine +
                "    <ViuBundleCssTaskAssembly>" +
                EscapeAttribute(bundleTaskAssemblyPath) +
                "</ViuBundleCssTaskAssembly>" + Environment.NewLine +
                "    <ViuCssHotReloadTaskAssembly>" +
                EscapeAttribute(browserTaskAssemblyPath) +
                "</ViuCssHotReloadTaskAssembly>" + Environment.NewLine +
                "    <ViuCssHotReloadWorkerAssembly>" +
                EscapeAttribute(workerAssemblyPath) +
                "</ViuCssHotReloadWorkerAssembly>" + Environment.NewLine +
                "    <ViuCssHotReloadStateFile>" +
                EscapeAttribute(statePath) +
                "</ViuCssHotReloadStateFile>" + Environment.NewLine +
                "    <ViuCssHotReloadEventLog>" +
                EscapeAttribute(eventLogPath) +
                "</ViuCssHotReloadEventLog>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <ItemGroup>" + Environment.NewLine +
                "    <ViuSingleFileComponent Include=\"App.vue\" />" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine +
                "  <Target Name=\"ResolveStaticWebAssetsConfiguration\" />" + Environment.NewLine +
                "  <Import Project=\"" + EscapeAttribute(componentTargetsPath) + "\" />" + Environment.NewLine +
                "  <Import Project=\"" + EscapeAttribute(hotReloadTargetsPath) + "\" />" + Environment.NewLine +
                "  <Target Name=\"ProbeWatch\" DependsOnTargets=\"_ViuCollectCssHotReloadWatchItems\">" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(ProbeOutput)\" Lines=\"@(Watch->'%(FullPath)|%(StaticWebAssetPath)')\" Overwrite=\"true\" />" + Environment.NewLine +
                "  </Target>" + Environment.NewLine +
                "</Project>" + Environment.NewLine;
            File.WriteAllText(
                projectPath,
                projectText,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return new TestContext(
                directoryPath,
                projectPath,
                indexPath,
                componentPath,
                Path.Combine(directoryPath, "obj", "viu", "Probe.viu.css"),
                statePath,
                eventLogPath,
                workerAssemblyPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static string FindRepositoryDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Assimalign.Viu.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("The Viu repository root could not be located.");
        }

        private static string FindOutputFile(
            string repositoryDirectory,
            string fileName)
        {
            var sdkOutputDirectory = Path.Combine(
                repositoryDirectory,
                "_out",
                "dotnet",
                "sdk");
            return Directory.GetFiles(
                    sdkOutputDirectory,
                    fileName,
                    SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .First();
        }

        private static string EscapeAttribute(string value) =>
            value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
