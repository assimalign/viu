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
    public void WatchCollection_DebugDotNetWatch_RegistersGeneratedStylesheetsAsStaticFiles()
    {
        var context = TestContext.Create(includeComponentStyles: true);
        try
        {
            File.Exists(context.UtilityBundlePath).ShouldBeFalse();
            File.Exists(
                Path.Combine(
                    context.DirectoryPath,
                    "obj",
                    "viu",
                    "Probe.viu.css")).ShouldBeFalse();
            var debugResultPath = Path.Combine(context.DirectoryPath, "debug-watch.txt");
            RunMsBuild(
                context,
                "ProbeWatch",
                redirectOutput: true,
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
            debugLines.ShouldContain(
                line => line.EndsWith(
                    "Probe.utilities.css|wwwroot/Probe.utilities.css",
                    StringComparison.OrdinalIgnoreCase));
            File.Exists(context.UtilityBundlePath).ShouldBeTrue();
            new FileInfo(context.UtilityBundlePath).Length.ShouldBe(0);
            File.Exists(
                context.UtilityBundlePath + ".hot-reload-empty").ShouldBeTrue();

            var releaseResultPath = Path.Combine(context.DirectoryPath, "release-watch.txt");
            RunMsBuild(
                context,
                "ProbeWatch",
                redirectOutput: true,
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
                redirectOutput: true,
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
    public void Worker_ExternalReferenceAndSourceRootChanges_RegeneratesUtilityBundle()
    {
        var context = TestContext.Create(includeComponentStyles: false);
        var externalDirectory = context.DirectoryPath + "-external";
        Process? workerProcess = null;
        try
        {
            Directory.CreateDirectory(externalDirectory);
            var externalSourcePath = Path.Combine(
                externalDirectory,
                "External.html");
            var referencePath = Path.Combine(
                externalDirectory,
                "Theme.css");
            File.WriteAllText(
                externalSourcePath,
                "<div class=\"bg-shared\"></div>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                referencePath,
                "@theme { --color-shared: #112233; }",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var entryPath = Path.Combine(
                context.DirectoryPath,
                "Utilities.css");
            var externalSpecifier = Path.GetRelativePath(
                    context.DirectoryPath,
                    externalDirectory)
                .Replace('\\', '/');
            File.WriteAllText(
                entryPath,
                "@import \"viu-utilities\" source(\"" +
                externalSpecifier +
                "\");" +
                Environment.NewLine +
                "@reference \"" +
                externalSpecifier +
                "/Theme.css\";",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var project = File.ReadAllText(context.ProjectPath);
            project = project.Replace(
                "    <ViuSingleFileComponent Include=\"App.vue\" />",
                "    <ViuSingleFileComponent Include=\"App.vue\" />" +
                Environment.NewLine +
                "    <ViuUtilityCss Include=\"Utilities.css\" />",
                StringComparison.Ordinal);
            File.WriteAllText(
                context.ProjectPath,
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                redirectOutput: true,
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            File.ReadAllText(context.UtilityBundlePath)
                .ShouldContain("#112233");
            File.Exists(context.DependencyManifestPath).ShouldBeTrue();

            workerProcess = StartWorker(context);
            WaitFor(() => File.Exists(context.StatePath), "worker state file");

            WriteWatchedFile(
                referencePath,
                "@theme { --color-shared: #445566; }");
            WaitForEventCount(context.EventLogPath, 1);
            WaitFor(
                () => File.ReadAllText(context.UtilityBundlePath)
                    .Contains("#445566", StringComparison.Ordinal),
                "referenced stylesheet regeneration");

            using (var retainedEventLogReader = new FileStream(
                       context.EventLogPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                WriteWatchedFile(
                    externalSourcePath,
                    "<div class=\"hidden\"></div>");
                Thread.Sleep(500);
            }

            WaitForEventCount(context.EventLogPath, 2);
            WaitFor(
                () => File.ReadAllText(context.UtilityBundlePath)
                    .Contains(".hidden", StringComparison.Ordinal),
                "external source-root regeneration");
            File.ReadAllText(context.UtilityBundlePath)
                .ShouldNotContain(".bg-shared");
        }
        finally
        {
            StopWorker(workerProcess);
            context.Dispose();
            if (Directory.Exists(externalDirectory))
            {
                Directory.Delete(
                    externalDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void Worker_FirstCandidate_CreatesPreviouslyAbsentWatchedBundle()
    {
        var context = TestContext.Create(includeComponentStyles: false);
        Process? workerProcess = null;
        try
        {
            File.Exists(context.UtilityBundlePath).ShouldBeFalse();
            workerProcess = StartWorker(context);
            WaitFor(() => File.Exists(context.StatePath), "worker state file");

            File.WriteAllText(
                Path.Combine(context.DirectoryPath, "Candidate.html"),
                "<div class=\"flex\"></div>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            WaitForEventCount(context.EventLogPath, 1);

            File.Exists(context.UtilityBundlePath).ShouldBeTrue();
            File.ReadAllText(context.UtilityBundlePath).ShouldContain(".flex");

            File.Delete(context.StatePath);
            WaitFor(
                () =>
                {
                    workerProcess.Refresh();
                    return workerProcess.HasExited;
                },
                "worker shutdown after state-file removal");
        }
        finally
        {
            StopWorker(workerProcess);
            context.Dispose();
        }
    }

    [Fact]
    public void Worker_ChangesNoOpRemovalAndShutdown_PreservesIncrementalContract()
    {
        var context = TestContext.Create(includeComponentStyles: false);
        Process? workerProcess = null;
        try
        {
            File.WriteAllText(
                context.IndexPath,
                "<div class=\"flex\"></div>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                redirectOutput: true,
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });

            File.Exists(context.UtilityBundlePath).ShouldBeTrue();
            var originalCss = File.ReadAllText(context.UtilityBundlePath);
            var originalWriteTime = File.GetLastWriteTimeUtc(context.UtilityBundlePath);

            WriteWatchedFile(
                context.IndexPath,
                "<div   class=\"flex\"></div>");
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                redirectOutput: true,
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            File.ReadAllText(context.UtilityBundlePath).ShouldBe(originalCss);
            File.GetLastWriteTimeUtc(context.UtilityBundlePath).ShouldBe(originalWriteTime);

            var changedSourcePath = Path.Combine(
                context.DirectoryPath,
                "Changed.html");
            File.WriteAllText(
                changedSourcePath,
                "<div class=\"hidden block\"></div>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                redirectOutput: true,
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            var changedCss = File.ReadAllText(context.UtilityBundlePath);
            changedCss.ShouldNotBe(originalCss);
            changedCss.ShouldContain(".hidden");

            File.Delete(changedSourcePath);
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                redirectOutput: true,
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            var partialDeletionCss = File.ReadAllText(context.UtilityBundlePath);
            partialDeletionCss.ShouldContain(".flex");
            partialDeletionCss.ShouldNotContain(".hidden");
            partialDeletionCss.ShouldNotContain(".block");

            File.Delete(context.IndexPath);
            File.WriteAllText(
                Path.Combine(context.DirectoryPath, "Empty.html"),
                "<div></div>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            RunMsBuild(
                context,
                "_ViuRegenerateCssHotReloadBundles",
                redirectOutput: true,
                new Dictionary<string, string>
                {
                    ["ViuCssHotReloadWorker"] = "true",
                });
            new FileInfo(context.UtilityBundlePath).Length.ShouldBe(0);
            File.Exists(context.UtilityBundlePath + ".hot-reload-empty").ShouldBeTrue();

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

            RunMsBuild(
                context,
                "_ViuBundleUtilityCss",
                redirectOutput: true,
                new Dictionary<string, string>());
            File.Exists(context.UtilityBundlePath).ShouldBeFalse();
            File.Exists(context.UtilityBundlePath + ".hot-reload-empty").ShouldBeFalse();
        }
        finally
        {
            StopWorker(workerProcess);
            context.Dispose();
        }
    }

    [Fact]
    public void Bundle_CssFirstEntry_ExecutesImportThemeSourcesReferencesAndCustomRules()
    {
        var context = TestContext.Create(includeComponentStyles: false);
        try
        {
            var entryPath = Path.Combine(
                context.DirectoryPath,
                "Utilities.css");
            var referencePath = Path.Combine(
                context.DirectoryPath,
                "Shared.css");
            var explicitSourcePath = Path.Combine(
                context.DirectoryPath,
                "Explicit.html");
            var automaticSourcePath = Path.Combine(
                context.DirectoryPath,
                "Automatic.html");
            File.WriteAllText(
                entryPath,
                """
                @charset "UTF-8";
                @import url("data:text/css,.imported%7Bcolor:red%7D");
                @import "viu-utilities" source(none) prefix(vu) theme(static) important;
                @reference "./Shared.css";
                @source inline("vu:flex");
                @source inline("vu:night:badge-shared");
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                referencePath,
                """
                @theme {
                  --color-shared: oklch(0.63 0.19 260);
                }
                @custom-variant night (&:where(.night *));
                @utility badge-* {
                  color: --value(--color-*);
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                explicitSourcePath,
                """<div class="vu:hidden"></div>""",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                automaticSourcePath,
                """<div class="vu:block"></div>""",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var project = File.ReadAllText(context.ProjectPath);
            project = project.Replace(
                "    <ViuSingleFileComponent Include=\"App.vue\" />",
                "    <ViuSingleFileComponent Include=\"App.vue\" />" +
                Environment.NewLine +
                "    <ViuUtilityCss Include=\"Utilities.css\" />" +
                Environment.NewLine +
                "    <ViuUtilityCssSource Include=\"Explicit.html\" />",
                StringComparison.Ordinal);
            File.WriteAllText(
                context.ProjectPath,
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            RunMsBuild(
                context,
                "_ViuBundleUtilityCss",
                redirectOutput: true,
                new Dictionary<string, string>());

            var bundle = File.ReadAllText(context.UtilityBundlePath);
            bundle.ShouldStartWith("@charset \"UTF-8\";");
            bundle.IndexOf(
                    "data:text/css",
                    StringComparison.Ordinal)
                .ShouldBeLessThan(
                    bundle.IndexOf(
                        "@layer theme",
                        StringComparison.Ordinal));
            bundle.ShouldContain("--vu-color-red-500:");
            bundle.ShouldContain(".vu\\:flex");
            bundle.ShouldContain("display: flex !important;");
            bundle.ShouldContain(".vu\\:hidden");
            bundle.ShouldNotContain(".vu\\:block");
            bundle.ShouldContain("--vu-color-shared:");
            bundle.ShouldContain(".vu\\:night\\:badge-shared");
            bundle.ShouldContain("color: var(--vu-color-shared) !important;");
            bundle.ShouldContain(":where(.night *)");
            bundle.ShouldNotContain("@source");
            bundle.ShouldNotContain("@reference");
            bundle.ShouldNotContain("\"viu-utilities\"");
        }
        finally
        {
            context.Dispose();
        }
    }

    [Fact]
    public void Bundle_ImportSourceRootAndExclusion_ReplacesAutomaticTemplateDiscovery()
    {
        var context = TestContext.Create(includeComponentStyles: false);
        try
        {
            var alternativeDirectory = Path.Combine(
                context.DirectoryPath,
                "Alternative");
            var legacyDirectory = Path.Combine(
                alternativeDirectory,
                "Legacy");
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(
                Path.Combine(context.DirectoryPath, "Utilities.css"),
                """
                @import "viu-utilities" source("./Alternative");
                @source not "./Alternative/Legacy";
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(alternativeDirectory, "Keep.vue"),
                """
                <template><div class="flex"></div></template>
                <script setup lang="csharp">
                private string Incidental => "hidden";
                </script>
                <style>.block { display: block; }</style>
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(legacyDirectory, "Skip.vue"),
                """<template><div class="hidden"></div></template>""",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(context.DirectoryPath, "Automatic.html"),
                """<div class="block"></div>""",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var project = File.ReadAllText(context.ProjectPath);
            project = project.Replace(
                "    <ViuSingleFileComponent Include=\"App.vue\" />",
                "    <ViuSingleFileComponent Include=\"App.vue\" />" +
                Environment.NewLine +
                "    <ViuUtilityCss Include=\"Utilities.css\" />",
                StringComparison.Ordinal);
            File.WriteAllText(
                context.ProjectPath,
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            RunMsBuild(
                context,
                "_ViuBundleUtilityCss",
                redirectOutput: true,
                new Dictionary<string, string>());

            var bundle = File.ReadAllText(context.UtilityBundlePath);
            bundle.ShouldContain(".flex");
            bundle.ShouldNotContain(".hidden");
            bundle.ShouldNotContain(".block");
        }
        finally
        {
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
        startInfo.ArgumentList.Add("--dependency-manifest");
        startInfo.ArgumentList.Add(context.DependencyManifestPath);
        startInfo.ArgumentList.Add("--owner-process-id");
        startInfo.ArgumentList.Add(
            currentProcess.Id.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--watch-utility-markup");
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
        bool redirectOutput,
        IReadOnlyDictionary<string, string> properties)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetDotNetHostPath(),
            WorkingDirectory = context.DirectoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            CreateNoWindow = redirectOutput,
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
        string standardOutput;
        string standardError;
        if (redirectOutput)
        {
            standardOutput = process.StandardOutput.ReadToEnd();
            standardError = process.StandardError.ReadToEnd();
        }
        else
        {
            standardOutput = string.Empty;
            standardError = string.Empty;
        }

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
            string utilityBundlePath,
            string dependencyManifestPath,
            string statePath,
            string eventLogPath,
            string workerAssemblyPath)
        {
            DirectoryPath = directoryPath;
            ProjectPath = projectPath;
            IndexPath = indexPath;
            UtilityBundlePath = utilityBundlePath;
            DependencyManifestPath = dependencyManifestPath;
            StatePath = statePath;
            EventLogPath = eventLogPath;
            WorkerAssemblyPath = workerAssemblyPath;
        }

        public string DirectoryPath { get; }

        public string ProjectPath { get; }

        public string IndexPath { get; }

        public string UtilityBundlePath { get; }

        public string DependencyManifestPath { get; }

        public string StatePath { get; }

        public string EventLogPath { get; }

        public string WorkerAssemblyPath { get; }

        public static TestContext Create(bool includeComponentStyles)
        {
            var repositoryDirectory = FindRepositoryDirectory();
            var taskAssemblyPath = FindOutputFile(
                repositoryDirectory,
                "Assimalign.Viu.Sdk.Tasks.dll");
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
            var utilityTargetsPath = Path.Combine(
                repositoryDirectory,
                "build",
                "Targets",
                "Build.UtilityCss.targets");
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
                "    <ViuBundleSingleFileComponentCss>" +
                includeComponentStyles.ToString().ToLowerInvariant() +
                "</ViuBundleSingleFileComponentCss>" + Environment.NewLine +
                "    <ViuBundleUtilityCss>true</ViuBundleUtilityCss>" + Environment.NewLine +
                "    <ViuBundleCssTaskAssembly>" +
                EscapeAttribute(taskAssemblyPath) +
                "</ViuBundleCssTaskAssembly>" + Environment.NewLine +
                "    <ViuUtilityCssTaskAssembly>" +
                EscapeAttribute(taskAssemblyPath) +
                "</ViuUtilityCssTaskAssembly>" + Environment.NewLine +
                "    <ViuCssHotReloadTaskAssembly>" +
                EscapeAttribute(taskAssemblyPath) +
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
                "  <Import Project=\"" + EscapeAttribute(utilityTargetsPath) + "\" />" + Environment.NewLine +
                "  <Import Project=\"" + EscapeAttribute(hotReloadTargetsPath) + "\" />" + Environment.NewLine +
                "  <Target Name=\"ProbeWatch\" DependsOnTargets=\"_ViuCollectCssHotReloadWatchItems\">" + Environment.NewLine +
                "    <WriteLinesToFile File=\"$(ProbeOutput)\" Lines=\"@(Watch->'%(FullPath)|%(StaticWebAssetPath)')\" Overwrite=\"true\" />" + Environment.NewLine +
                "  </Target>" + Environment.NewLine +
                "  <Target Name=\"ProbeLaunch\" DependsOnTargets=\"_ViuCollectCssHotReloadWatchItems\" />" + Environment.NewLine +
                "</Project>" + Environment.NewLine;
            File.WriteAllText(
                projectPath,
                projectText,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return new TestContext(
                directoryPath,
                projectPath,
                indexPath,
                Path.Combine(directoryPath, "obj", "viu", "Probe.utilities.css"),
                Path.Combine(
                    directoryPath,
                    "obj",
                    "viu",
                    "Probe.utilities.css.hot-reload-dependencies"),
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
