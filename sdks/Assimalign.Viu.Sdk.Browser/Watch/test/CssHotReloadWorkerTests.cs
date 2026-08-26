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
    // [V01.01.12.33], #356: dotnet watch must see the collector before it captures
    // CustomCollectWatchItems while importing Microsoft.Common targets.
    [Fact]
    public void WatchCollection_EarlyBrowserImport_RegistersComponentStylesheetAsStaticFile()
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

            var genericResultPath = Path.Combine(context.DirectoryPath, "generic-watch.txt");
            RunMsBuild(
                context,
                "ProbeWatch",
                new Dictionary<string, string>
                {
                    ["DotNetWatchBuild"] = "true",
                    ["DesignTimeBuild"] = "true",
                    ["ViuCssHotReloadLaunchWorker"] = "false",
                    ["ProbeRegisterGeneratedAsset"] = "true",
                    ["ProbeOutput"] = genericResultPath,
                });
            File.ReadAllLines(genericResultPath).ShouldContain(
                line => line.EndsWith(
                    "first.generated.css|wwwroot/probe.generated.css",
                    StringComparison.OrdinalIgnoreCase));

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
                "ViuGenerateSingleFileComponentCss",
                new Dictionary<string, string>
                {
                    ["ViuGeneratedAssetHotReload"] = "true",
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
                "ViuGenerateSingleFileComponentCss",
                new Dictionary<string, string>
                {
                    ["ViuGeneratedAssetHotReload"] = "true",
                });

            var originalCss = File.ReadAllText(context.BundlePath);
            var originalWriteTime = File.GetLastWriteTimeUtc(context.BundlePath);

            RunMsBuild(
                context,
                "ViuGenerateSingleFileComponentCss",
                new Dictionary<string, string>
                {
                    ["ViuGeneratedAssetHotReload"] = "true",
                });
            File.ReadAllText(context.BundlePath).ShouldBe(originalCss);
            File.GetLastWriteTimeUtc(context.BundlePath).ShouldBe(originalWriteTime);

            File.WriteAllText(
                context.ComponentPath,
                "<template><div /></template>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            RunMsBuild(
                context,
                "ViuGenerateSingleFileComponentCss",
                new Dictionary<string, string>
                {
                    ["ViuGeneratedAssetHotReload"] = "true",
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

    // [V01.01.12.30.04], #355 pins generic graph refresh and batched regeneration.
    [Fact]
    public void Worker_GenericGraphChanges_BatchesTargetsAndRefreshesManifestRoots()
    {
        var context = TestContext.Create();
        Process? workerProcess = null;
        try
        {
            var initialManifestRoot = Path.Combine(
                context.ExternalDirectoryPath,
                "initial-manifest-root");
            Directory.CreateDirectory(initialManifestRoot);
            var initialManifestSource = Path.Combine(
                initialManifestRoot,
                "initial.candidate");
            File.WriteAllText(initialManifestSource, "initial");
            WriteDependencyManifest(
                context.DependencyManifestPath,
                new[] { initialManifestSource },
                new[] { initialManifestRoot });

            var assets = new[]
            {
                new WorkerAsset(
                    context.FirstGeneratedAssetPath,
                    Array.Empty<string>(),
                    new[] { context.ExternalDirectoryPath },
                    new[] { ".utility" },
                    "GenerateFirstAsset",
                    string.Empty,
                    "wwwroot/first.generated.css",
                    "Delete"),
                new WorkerAsset(
                    context.SecondGeneratedAssetPath,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new[] { ".candidate" },
                    "GenerateSecondAsset",
                    context.DependencyManifestPath,
                    "wwwroot/second.generated.css",
                    "Delete"),
                new WorkerAsset(
                    context.ThirdGeneratedAssetPath,
                    new[] { context.GenericInputPath },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "generatefirstasset",
                    string.Empty,
                    "wwwroot/third.generated.css",
                    "Delete"),
            };

            workerProcess = StartWorker(context, assets);
            WaitFor(() => File.Exists(context.StatePath), "worker state file");

            File.WriteAllText(
                Path.Combine(context.ExternalDirectoryPath, "ignored.txt"),
                "ignored");
            Thread.Sleep(750);
            CountCompletedEvents(context.EventLogPath).ShouldBe(0);

            WriteWatchedFile(context.GenericInputPath, "second utility value");
            WaitForCompletedEventCount(context.EventLogPath, 1);
            CountSettledEvents(context.EventLogPath).ShouldBe(0);
            File.Delete(initialManifestSource);
            WaitForEventCount(context.EventLogPath, 2);
            File.ReadAllText(context.FirstGeneratedAssetPath)
                .ShouldContain("second utility value");
            File.ReadAllText(context.ThirdGeneratedAssetPath)
                .ShouldContain("second utility value");
            File.ReadAllText(context.EventLogPath).ShouldContain(
                "targets:GenerateFirstAsset;GenerateSecondAsset");
            CountTargetInvocations(context.TargetInvocationLogPath, "first").ShouldBe(2);
            CountTargetInvocations(context.TargetInvocationLogPath, "second").ShouldBe(2);

            var replacementManifestRoot = Path.Combine(
                context.ExternalDirectoryPath,
                "replacement-manifest-root");
            var replacementManifestSource = Path.Combine(
                replacementManifestRoot,
                "replacement.candidate");
            var missingManifestSource = Path.Combine(
                context.ExternalDirectoryPath,
                "currently-missing.candidate");
            WriteDependencyManifest(
                context.DependencyManifestPath,
                new[] { missingManifestSource },
                new[] { replacementManifestRoot });
            WaitForEventCount(context.EventLogPath, 3);

            Directory.CreateDirectory(replacementManifestRoot);
            WaitForEventCount(context.EventLogPath, 4);

            File.WriteAllText(replacementManifestSource, "replacement");
            WaitForEventCount(context.EventLogPath, 5);

            File.Delete(replacementManifestSource);
            WaitForEventCount(context.EventLogPath, 6);

            File.WriteAllText(missingManifestSource, "now present");
            WaitForEventCount(context.EventLogPath, 7);
            CountTargetInvocations(context.TargetInvocationLogPath, "first").ShouldBe(7);
            CountTargetInvocations(context.TargetInvocationLogPath, "second").ShouldBe(7);
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

    private static Process StartWorker(TestContext context) =>
        StartWorker(
            context,
            new[]
            {
                new WorkerAsset(
                    context.BundlePath,
                    Array.Empty<string>(),
                    new[] { context.DirectoryPath },
                    new[] { ".viu", ".vue" },
                    "ViuGenerateSingleFileComponentCss",
                    string.Empty,
                    "wwwroot/Probe.viu.css",
                    "PreserveEmpty"),
            });

    private static Process StartWorker(
        TestContext context,
        IReadOnlyList<WorkerAsset> assets)
    {
        using var currentProcess = Process.GetCurrentProcess();
        var configurationFilePath = context.StatePath + ".configuration";
        WriteWorkerConfiguration(
            configurationFilePath,
            context,
            currentProcess.Id,
            assets);
        var startInfo = new ProcessStartInfo
        {
            FileName = GetDotNetHostPath(),
            WorkingDirectory = context.DirectoryPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        startInfo.ArgumentList.Add(context.WorkerAssemblyPath);
        startInfo.ArgumentList.Add("--configuration-file");
        startInfo.ArgumentList.Add(configurationFilePath);
        return Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "The Generated Asset Hot Reload worker could not be started.");
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
                () => CountCompletedEvents(eventLogPath) >= count &&
                    CountSettledEvents(eventLogPath) >= count,
                count.ToString(CultureInfo.InvariantCulture) +
                    " completed and settled regenerations");
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

    private static void WaitForCompletedEventCount(string eventLogPath, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (CountCompletedEvents(eventLogPath) >= count)
            {
                return;
            }

            Thread.Sleep(1);
        }

        throw new TimeoutException(
            "Timed out waiting for " +
            count.ToString(CultureInfo.InvariantCulture) +
            " completed regenerations.");
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

    private static int CountSettledEvents(string eventLogPath)
    {
        try
        {
            return !File.Exists(eventLogPath)
                ? 0
                : File.ReadAllLines(eventLogPath)
                    .Count(line => string.Equals(line, "settled", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static int CountTargetInvocations(string path, string target) =>
        !File.Exists(path)
            ? 0
            : File.ReadAllLines(path)
                .Count(line => string.Equals(line, target, StringComparison.Ordinal));

    private static void WriteDependencyManifest(
        string path,
        IEnumerable<string> files,
        IEnumerable<string> roots)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = new List<string> { "viu-generated-asset-dependencies-v1" };
        lines.AddRange(files.Select(
            value => "file:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
        lines.AddRange(roots.Select(
            value => "root:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
        WriteWatchedFile(
            path,
            string.Join(Environment.NewLine, lines) + Environment.NewLine);
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

    private static void WriteWorkerConfiguration(
        string path,
        TestContext context,
        int ownerProcessIdentifier,
        IEnumerable<WorkerAsset> assets)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = new List<string>
        {
            "viu-generated-asset-worker-configuration-v1",
            EncodeConfigurationValue("project-path", context.ProjectPath),
            EncodeConfigurationValue("project-directory", context.DirectoryPath),
            EncodeConfigurationValue("dotnet-host", GetDotNetHostPath()),
            EncodeConfigurationValue("configuration", "Debug"),
            EncodeConfigurationValue("target-framework", string.Empty),
            EncodeConfigurationValue("runtime-identifier", string.Empty),
            EncodeConfigurationValue("state-file", context.StatePath),
            EncodeConfigurationValue("event-log", context.EventLogPath),
            EncodeConfigurationValue(
                "owner-process-identifier",
                ownerProcessIdentifier.ToString(CultureInfo.InvariantCulture)),
            EncodeConfigurationValue("debounce-milliseconds", "50"),
            EncodeConfigurationValue(
                "excluded-directory",
                Path.Combine(context.DirectoryPath, "obj")),
            EncodeConfigurationValue(
                "excluded-directory",
                Path.Combine(context.DirectoryPath, "bin")),
        };

        foreach (var asset in assets)
        {
            lines.Add("asset-begin");
            lines.Add(EncodeConfigurationValue("identity", asset.Identity));
            lines.AddRange(asset.WatchFiles.Select(
                value => EncodeConfigurationValue("watch-file", value)));
            lines.AddRange(asset.WatchRoots.Select(
                value => EncodeConfigurationValue("watch-root", value)));
            lines.AddRange(asset.WatchExtensions.Select(
                value => EncodeConfigurationValue("watch-extension", value)));
            lines.Add(EncodeConfigurationValue(
                "regeneration-target",
                asset.RegenerationTarget));
            if (!string.IsNullOrEmpty(asset.DependencyManifestPath))
            {
                lines.Add(EncodeConfigurationValue(
                    "dependency-manifest-path",
                    asset.DependencyManifestPath));
            }

            lines.Add(EncodeConfigurationValue(
                "static-web-asset-path",
                asset.StaticWebAssetPath));
            lines.Add(EncodeConfigurationValue(
                "removal-behavior",
                asset.RemovalBehavior));
            lines.Add("asset-end");
        }

        File.WriteAllLines(
            path,
            lines,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EncodeConfigurationValue(string name, string value) =>
        name + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private sealed record WorkerAsset(
        string Identity,
        IReadOnlyList<string> WatchFiles,
        IReadOnlyList<string> WatchRoots,
        IReadOnlyList<string> WatchExtensions,
        string RegenerationTarget,
        string DependencyManifestPath,
        string StaticWebAssetPath,
        string RemovalBehavior);

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

        public string ExternalDirectoryPath => DirectoryPath + "-external";

        public string GenericInputPath =>
            Path.Combine(ExternalDirectoryPath, "input.utility");

        public string DependencyManifestPath =>
            Path.Combine(DirectoryPath, "obj", "dependencies.manifest");

        public string FirstGeneratedAssetPath =>
            Path.Combine(DirectoryPath, "obj", "first.generated.css");

        public string SecondGeneratedAssetPath =>
            Path.Combine(DirectoryPath, "obj", "second.generated.css");

        public string ThirdGeneratedAssetPath =>
            Path.Combine(DirectoryPath, "obj", "third.generated.css");

        public string TargetInvocationLogPath =>
            Path.Combine(DirectoryPath, "obj", "target-invocations.log");

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
            var externalDirectoryPath = directoryPath + "-external";
            Directory.CreateDirectory(externalDirectoryPath);
            var genericInputPath = Path.Combine(
                externalDirectoryPath,
                "input.utility");
            File.WriteAllText(genericInputPath, "first utility value");
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
            var browserCommonPropsPath = Path.Combine(
                repositoryDirectory,
                "sdks",
                "Assimalign.Viu.Sdk.Browser",
                "Targets",
                "Assimalign.Viu.Sdk.Browser.Common.props");
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
                "    <GenericInputPath>" +
                EscapeAttribute(genericInputPath) +
                "</GenericInputPath>" + Environment.NewLine +
                "    <FirstGeneratedAssetPath>" +
                EscapeAttribute(Path.Combine(directoryPath, "obj", "first.generated.css")) +
                "</FirstGeneratedAssetPath>" + Environment.NewLine +
                "    <SecondGeneratedAssetPath>" +
                EscapeAttribute(Path.Combine(directoryPath, "obj", "second.generated.css")) +
                "</SecondGeneratedAssetPath>" + Environment.NewLine +
                "    <ThirdGeneratedAssetPath>" +
                EscapeAttribute(Path.Combine(directoryPath, "obj", "third.generated.css")) +
                "</ThirdGeneratedAssetPath>" + Environment.NewLine +
                "    <TargetInvocationLogPath>" +
                EscapeAttribute(Path.Combine(directoryPath, "obj", "target-invocations.log")) +
                "</TargetInvocationLogPath>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <ItemGroup>" + Environment.NewLine +
                "    <ViuSingleFileComponent Include=\"App.vue\" />" + Environment.NewLine +
                "  </ItemGroup>" + Environment.NewLine +
                "  <Target Name=\"ResolveStaticWebAssetsConfiguration\" />" + Environment.NewLine +
                "  <Import Project=\"" + EscapeAttribute(browserCommonPropsPath) + "\" />" + Environment.NewLine +
                "  <PropertyGroup>" + Environment.NewLine +
                "    <_CapturedCustomCollectWatchItems>$(CustomCollectWatchItems)</_CapturedCustomCollectWatchItems>" + Environment.NewLine +
                "  </PropertyGroup>" + Environment.NewLine +
                "  <Import Project=\"" + EscapeAttribute(componentTargetsPath) + "\" />" + Environment.NewLine +
                "  <Import Project=\"" + EscapeAttribute(hotReloadTargetsPath) + "\" />" + Environment.NewLine +
                "  <Target Name=\"RegisterProbeGeneratedAsset\" BeforeTargets=\"ViuCollectGeneratedAssets\" Condition=\"'$(ProbeRegisterGeneratedAsset)' == 'true'\">" + Environment.NewLine +
                "    <ItemGroup>" + Environment.NewLine +
                "      <ViuGeneratedAsset Include=\"$(FirstGeneratedAssetPath)\">" + Environment.NewLine +
                "        <WatchFiles>$(GenericInputPath)</WatchFiles>" + Environment.NewLine +
                "        <RegenerationTarget>GenerateFirstAsset</RegenerationTarget>" + Environment.NewLine +
                "        <StaticWebAssetPath>wwwroot/probe.generated.css</StaticWebAssetPath>" + Environment.NewLine +
                "        <RemovalBehavior>Delete</RemovalBehavior>" + Environment.NewLine +
                "      </ViuGeneratedAsset>" + Environment.NewLine +
                "    </ItemGroup>" + Environment.NewLine +
                "  </Target>" + Environment.NewLine +
                "  <Target Name=\"GenerateFirstAsset\">" + Environment.NewLine +
                "    <ReadLinesFromFile File=\"$(GenericInputPath)\"><Output TaskParameter=\"Lines\" ItemName=\"_GenericFirstLines\" /></ReadLinesFromFile>" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(FirstGeneratedAssetPath)\" Lines=\"@(_GenericFirstLines)\" Overwrite=\"true\" WriteOnlyWhenDifferent=\"true\" />" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(ThirdGeneratedAssetPath)\" Lines=\"@(_GenericFirstLines)\" Overwrite=\"true\" WriteOnlyWhenDifferent=\"true\" />" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(TargetInvocationLogPath)\" Lines=\"first\" Overwrite=\"false\" />" + Environment.NewLine +
                "  </Target>" + Environment.NewLine +
                "  <Target Name=\"GenerateSecondAsset\">" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(SecondGeneratedAssetPath)\" Lines=\"second\" Overwrite=\"true\" WriteOnlyWhenDifferent=\"true\" />" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(TargetInvocationLogPath)\" Lines=\"second\" Overwrite=\"false\" />" + Environment.NewLine +
                "  </Target>" + Environment.NewLine +
                "  <Target Name=\"ProbeWatch\" DependsOnTargets=\"$(_CapturedCustomCollectWatchItems)\">" + Environment.NewLine +
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

            if (Directory.Exists(ExternalDirectoryPath))
            {
                Directory.Delete(ExternalDirectoryPath, recursive: true);
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
