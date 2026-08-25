using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Build.Tests;

// [V01.01.12.30] pins the standalone task's source routing, SFC slicing, CSS-first entry,
// deterministic-write, and stale-output contracts independently of any Viu SDK.
// [V01.01.12.30.04], #355 pins its public generated-asset dependency and removal contracts.
public sealed class ViuGenerateUtilityCssTests
{
    [Theory]
    [InlineData(".viu")]
    [InlineData(".vue")]
    [InlineData(".razor")]
    [InlineData(".cshtml")]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void Execute_SupportedSourceExtension_RoutesMarkupToCandidateScanner(
        string extension)
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "Source" + extension);
            File.WriteAllText(sourcePath, WrapMarkup(extension, "<div class=\"flex\"></div>"));
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");

            var task = CreateTask(projectDirectory, outputPath, sourcePath);

            task.Execute().ShouldBeTrue();
            File.ReadAllText(outputPath).ShouldContain("display: flex;");
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(".viu")]
    [InlineData(".vue")]
    public void Execute_SingleFileComponent_ScansOnlyTemplateContent(
        string extension)
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "Source" + extension);
            var markup = "<div class=\"bg-blue-500\"></div>";
            var source = extension == ".viu"
                ? $$"""
                    <template>
                    {{markup}}
                    </template>
                    @script {
                        private const string Decoy = "<span class=\"opacity-50\"></span>";
                    }
                    <style>
                    .decoy::after { content: "<span class=\"opacity-75\"></span>"; }
                    </style>
                    """
                : $$"""
                    <template>
                    {{markup}}
                    </template>
                    <script lang="csharp">
                    private const string Decoy = "<span class=\"opacity-50\"></span>";
                    </script>
                    <style>
                    .decoy::after { content: "<span class=\"opacity-75\"></span>"; }
                    </style>
                    """;
            File.WriteAllText(sourcePath, source);
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");

            var task = CreateTask(projectDirectory, outputPath, sourcePath);

            task.Execute().ShouldBeTrue();
            var css = File.ReadAllText(outputPath);
            css.ShouldContain("background-color: var(--color-blue-500);");
            css.ShouldNotContain("opacity: 0.5;");
            css.ShouldNotContain("opacity: 0.75;");
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_EntryStylesheetTheme_AppliesThemeToGeneratedRule()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var entryPath = Path.Combine(projectDirectory, "utilities.css");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(sourcePath, "<div class=\"bg-brand\"></div>");
            File.WriteAllText(
                entryPath,
                "@theme { --color-brand: #123456; }");
            var task = CreateTask(projectDirectory, outputPath, sourcePath);
            task.UtilityStylesheets = new ITaskItem[] { new TaskItem(entryPath) };

            task.Execute().ShouldBeTrue();
            var css = File.ReadAllText(outputPath);
            css.ShouldContain("--color-brand: #123456;");
            css.ShouldContain("background-color: var(--color-brand);");
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_EditorSidecar_DescribesResolvedInputsAndStructuredClassMetadata()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var additionalSourcePath = Path.Combine(projectDirectory, "about.html");
            var entryPath = Path.Combine(projectDirectory, "utilities.css");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(
                sourcePath,
                "<div class=\"bg-brand flex project-card hover:grid\"></div>");
            File.WriteAllText(
                additionalSourcePath,
                "<span class=\"block\"></span>");
            File.WriteAllText(
                entryPath,
                "@theme { --color-brand: #123456; } " +
                "@utility project-card { color: var(--color-brand); padding: 1rem; }");
            var task = CreateTask(
                projectDirectory,
                outputPath,
                sourcePath,
                additionalSourcePath);
            task.UtilityStylesheets = new ITaskItem[] { new TaskItem(entryPath) };

            task.Execute().ShouldBeTrue();

            using var manifest = JsonDocument.Parse(
                File.ReadAllBytes(GetManifestPath(outputPath)));
            var manifestRoot = manifest.RootElement;
            manifestRoot.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
            manifestRoot.GetProperty("engineVersion").GetString().ShouldNotBeNullOrWhiteSpace();
            manifestRoot.GetProperty("entryStylesheetPath").GetString().ShouldBe(
                Path.GetFullPath(entryPath));
            manifestRoot.GetProperty("sourceFiles")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ShouldBe(new string?[]
                {
                    Path.GetFullPath(additionalSourcePath),
                    Path.GetFullPath(sourcePath),
                });
            var themeContentHash = manifestRoot
                .GetProperty("themeContentHash")
                .GetString();
            themeContentHash.ShouldNotBeNull();
            themeContentHash!.Length.ShouldBe(64);
            themeContentHash.All(character =>
                    character is >= '0' and <= '9' or >= 'a' and <= 'f')
                .ShouldBeTrue();
            var bundle = manifestRoot.GetProperty("bundle");
            bundle.GetProperty("path").GetString().ShouldBe(
                Path.GetFullPath(outputPath));
            bundle.GetProperty("name").GetString().ShouldBe(
                Path.GetFileName(outputPath));
            File.Exists(bundle.GetProperty("path").GetString()).ShouldBeTrue();

            using var catalog = JsonDocument.Parse(
                File.ReadAllBytes(GetCatalogPath(outputPath)));
            var catalogRoot = catalog.RootElement;
            catalogRoot.GetProperty("version").GetInt32().ShouldBe(1);
            catalogRoot.GetProperty("truncated").GetBoolean().ShouldBeFalse();
            var entries = catalogRoot.GetProperty("entries");
            var classNames = entries
                .EnumerateArray()
                .Select(entry => entry.GetProperty("class").GetString()!)
                .ToArray();
            var sourceUsedClassNames = new HashSet<string>(
                new[] { "bg-brand", "block", "flex", "project-card" },
                StringComparer.Ordinal);
            classNames.Take(sourceUsedClassNames.Count)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(sourceUsedClassNames)
                .ShouldBeTrue();
            classNames.Skip(sourceUsedClassNames.Count)
                .ShouldBe(
                    classNames
                        .Skip(sourceUsedClassNames.Count)
                        .OrderBy(item => item, StringComparer.Ordinal));
            classNames.ShouldNotContain("hover:grid");
            classNames.ShouldNotContain("sm:block");
            classNames.ShouldContain("m-0");
            classNames.ShouldContain("m-0.5");
            classNames.ShouldContain("m-96");
            classNames.ShouldContain("m-auto");
            classNames.ShouldContain("m-px");
            classNames.ShouldContain("mx-auto");
            classNames.ShouldContain("-m-4");
            classNames.ShouldContain("inset-auto");
            classNames.ShouldContain("-top-full");
            classNames.ShouldContain("w-auto");
            classNames.ShouldContain("h-dvh");
            classNames.ShouldContain("grid-cols-12");
            classNames.ShouldContain("col-span-12");
            classNames.ShouldContain("object-top-left");
            classNames.ShouldContain("flex-1/2");
            classNames.ShouldContain("order-12");
            classNames.ShouldContain("z-20");
            classNames.ShouldContain("border-4");
            classNames.ShouldContain("opacity-25");
            classNames.ShouldContain("-rotate-45");
            classNames.ShouldContain("-translate-1/2");
            classNames.ShouldContain("-scale-105");
            classNames.ShouldNotContain("-m-auto");
            classNames.ShouldNotContain("-translate-none");
            classNames.ShouldContain("text-brand");
            classNames.ShouldContain("ring-brand");
            classNames.ShouldContain("mask-conic-to-brand");
            classNames.ShouldContain("stroke-brand");
            var colorEntry = FindCatalogEntry(entries, "bg-brand");
            colorEntry.GetProperty("css").GetString()!.ShouldContain(
                "background-color: var(--color-brand);");
            colorEntry.GetProperty("colorValue").GetString().ShouldBe("#123456");
            var nonColorEntry = FindCatalogEntry(entries, "flex");
            nonColorEntry.GetProperty("css").GetString()!.ShouldContain("display: flex;");
            nonColorEntry.TryGetProperty("colorValue", out _).ShouldBeFalse();
            FindCatalogEntry(entries, "project-card")
                .GetProperty("css")
                .GetString()!
                .ShouldContain("padding: 1rem;");
            FindCatalogEntry(entries, "sr-only")
                .GetProperty("class")
                .GetString()
                .ShouldBe("sr-only");

            File.WriteAllText(
                entryPath,
                "@theme { --color-brand: #654321; } " +
                "@utility project-card { color: var(--color-brand); padding: 1rem; }");
            var changedThemeTask = CreateTask(
                projectDirectory,
                outputPath,
                sourcePath,
                additionalSourcePath);
            changedThemeTask.UtilityStylesheets = new ITaskItem[] { new TaskItem(entryPath) };

            changedThemeTask.Execute().ShouldBeTrue();
            using var changedManifest = JsonDocument.Parse(
                File.ReadAllBytes(GetManifestPath(outputPath)));
            changedManifest.RootElement
                .GetProperty("themeContentHash")
                .GetString()
                .ShouldNotBe(themeContentHash);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_LateRankedGeneratedRuleWithOneItemBudget_RemainsInCatalogAndTruncates()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(sourcePath, "<div class=\"bg-blue-500\"></div>");
            var task = CreateTask(projectDirectory, outputPath, sourcePath);
            task.EditorCatalogMaximumItems = 1;

            task.Execute().ShouldBeTrue();

            using var catalog = JsonDocument.Parse(
                File.ReadAllBytes(GetCatalogPath(outputPath)));
            catalog.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
            var entries = catalog.RootElement.GetProperty("entries");
            entries.GetArrayLength().ShouldBe(1);
            entries[0].GetProperty("class").GetString().ShouldBe("bg-blue-500");
            entries[0].GetProperty("colorValue").GetString().ShouldBe("oklch(62.3% 0.214 259.815)");
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_UnchangedOutput_PreservesBytesAndTimestamp()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(sourcePath, "<div class=\"rounded-lg\"></div>");
            var firstTask = CreateTask(projectDirectory, outputPath, sourcePath);
            firstTask.Execute().ShouldBeTrue();
            firstTask.OutputWritten.ShouldBeTrue();
            var expectedBytes = File.ReadAllBytes(outputPath);
            var manifestPath = GetManifestPath(outputPath);
            var catalogPath = GetCatalogPath(outputPath);
            var expectedManifestBytes = File.ReadAllBytes(manifestPath);
            var expectedCatalogBytes = File.ReadAllBytes(catalogPath);
            var expectedTimestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(outputPath, expectedTimestamp);
            File.SetLastWriteTimeUtc(manifestPath, expectedTimestamp);
            File.SetLastWriteTimeUtc(catalogPath, expectedTimestamp);

            var secondTask = CreateTask(projectDirectory, outputPath, sourcePath);

            secondTask.Execute().ShouldBeTrue();
            secondTask.OutputWritten.ShouldBeFalse();
            File.ReadAllBytes(outputPath).ShouldBe(expectedBytes);
            File.ReadAllBytes(manifestPath).ShouldBe(expectedManifestBytes);
            File.ReadAllBytes(catalogPath).ShouldBe(expectedCatalogBytes);
            File.GetLastWriteTimeUtc(outputPath).ShouldBe(expectedTimestamp);
            File.GetLastWriteTimeUtc(manifestPath).ShouldBe(expectedTimestamp);
            File.GetLastWriteTimeUtc(catalogPath).ShouldBe(expectedTimestamp);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_NoRemainingRules_DeletesStaleOutput()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(sourcePath, "<div class=\"flex\"></div>");
            var firstTask = CreateTask(projectDirectory, outputPath, sourcePath);
            firstTask.Execute().ShouldBeTrue();
            File.Exists(outputPath).ShouldBeTrue();
            File.Exists(GetManifestPath(outputPath)).ShouldBeTrue();
            File.Exists(GetCatalogPath(outputPath)).ShouldBeTrue();
            File.WriteAllText(sourcePath, "<div>No utility candidate</div>");

            var secondTask = CreateTask(projectDirectory, outputPath, sourcePath);

            secondTask.Execute().ShouldBeTrue();
            secondTask.OutputExists.ShouldBeFalse();
            File.Exists(outputPath).ShouldBeFalse();
            File.Exists(GetManifestPath(outputPath)).ShouldBeFalse();
            File.Exists(GetCatalogPath(outputPath)).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_GeneratedAssetDependencyManifest_TracksEntrySourceReferenceAndRootClosure()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourceDirectory = Path.Combine(projectDirectory, "ExternalSources");
            Directory.CreateDirectory(sourceDirectory);
            var sourcePath = Path.Combine(sourceDirectory, "index.html");
            var entryPath = Path.Combine(projectDirectory, "utilities.css");
            var referencePath = Path.Combine(projectDirectory, "theme.css");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            var dependencyManifestPath = Path.Combine(
                projectDirectory,
                "obj",
                "utilitycss.generated-asset-dependencies.v1");
            File.WriteAllText(
                sourcePath,
                "<div class=\"bg-shared\"></div>");
            File.WriteAllText(
                referencePath,
                "@theme { --color-shared: #112233; }");
            File.WriteAllText(
                entryPath,
                "@import \"viu-utilities\" source(\"./ExternalSources\");" +
                Environment.NewLine +
                "@reference \"./theme.css\";");
            var task = CreateTask(projectDirectory, outputPath);
            task.UtilityStylesheets = new ITaskItem[] { new TaskItem(entryPath) };
            task.GeneratedAssetDependencyManifestPath = dependencyManifestPath;

            task.Execute().ShouldBeTrue();

            File.ReadAllText(outputPath).ShouldContain("#112233");
            var manifest = ReadGeneratedAssetDependencyManifest(
                dependencyManifestPath);
            manifest.Files.ShouldBe(
                new[]
                {
                    Path.GetFullPath(sourcePath),
                    Path.GetFullPath(referencePath),
                    Path.GetFullPath(entryPath),
                },
                ignoreOrder: true);
            manifest.Roots.ShouldBe(
                new[] { Path.GetFullPath(sourceDirectory) });
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_GeneratedAssetDependencyManifestUnchanged_PreservesTimestamp()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            var dependencyManifestPath = Path.Combine(
                projectDirectory,
                "obj",
                "utilitycss.generated-asset-dependencies.v1");
            File.WriteAllText(sourcePath, "<div class=\"flex\"></div>");
            var firstTask = CreateTask(projectDirectory, outputPath, sourcePath);
            firstTask.GeneratedAssetDependencyManifestPath = dependencyManifestPath;
            firstTask.Execute().ShouldBeTrue();
            var expectedBytes = File.ReadAllBytes(dependencyManifestPath);
            var expectedTimestamp = new DateTime(
                2025,
                1,
                2,
                3,
                4,
                5,
                DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(
                dependencyManifestPath,
                expectedTimestamp);
            var secondTask = CreateTask(projectDirectory, outputPath, sourcePath);
            secondTask.GeneratedAssetDependencyManifestPath = dependencyManifestPath;

            secondTask.Execute().ShouldBeTrue();

            File.ReadAllBytes(dependencyManifestPath).ShouldBe(expectedBytes);
            File.GetLastWriteTimeUtc(dependencyManifestPath).ShouldBe(
                expectedTimestamp);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_GeneratedAssetDependencyManifest_WatchFalseOmitsDirectInputsButRetainsReferenceClosure()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var watchedSourcePath = Path.Combine(projectDirectory, "watched.html");
            var unwatchedSourcePath = Path.Combine(projectDirectory, "unwatched.html");
            var entryPath = Path.Combine(projectDirectory, "utilities.css");
            var referencePath = Path.Combine(projectDirectory, "theme.css");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            var dependencyManifestPath = Path.Combine(
                projectDirectory,
                "obj",
                "utilitycss.generated-asset-dependencies.v1");
            File.WriteAllText(watchedSourcePath, "<div class=\"flex\"></div>");
            File.WriteAllText(unwatchedSourcePath, "<div class=\"grid\"></div>");
            File.WriteAllText(referencePath, "@theme { --color-shared: #112233; }");
            File.WriteAllText(entryPath, "@reference \"./theme.css\";");
            var unwatchedSource = new TaskItem(unwatchedSourcePath);
            unwatchedSource.SetMetadata("Watch", "false");
            var unwatchedEntry = new TaskItem(entryPath);
            unwatchedEntry.SetMetadata("Watch", "false");
            var task = CreateTask(
                projectDirectory,
                outputPath,
                watchedSourcePath);
            task.SourceFiles = new ITaskItem[]
            {
                new TaskItem(watchedSourcePath),
                unwatchedSource,
            };
            task.UtilityStylesheets = new ITaskItem[] { unwatchedEntry };
            task.GeneratedAssetDependencyManifestPath = dependencyManifestPath;

            task.Execute().ShouldBeTrue();

            var manifest = ReadGeneratedAssetDependencyManifest(
                dependencyManifestPath);
            manifest.Files.ShouldBe(
                new[]
                {
                    Path.GetFullPath(watchedSourcePath),
                    Path.GetFullPath(referencePath),
                },
                ignoreOrder: true);
            manifest.Roots.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_FinalRuleRemovedDuringGeneratedAssetHotReload_PreservesEmptyOutputUntilOrdinaryBuild()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            var dependencyManifestPath = Path.Combine(
                projectDirectory,
                "obj",
                "utilitycss.generated-asset-dependencies.v1");
            File.WriteAllText(sourcePath, "<div class=\"flex\"></div>");
            var firstTask = CreateTask(projectDirectory, outputPath, sourcePath);
            firstTask.GeneratedAssetDependencyManifestPath = dependencyManifestPath;
            firstTask.Execute().ShouldBeTrue();
            File.WriteAllText(sourcePath, "<div>No utility candidate</div>");
            var hotReloadTask = CreateTask(projectDirectory, outputPath, sourcePath);
            hotReloadTask.GeneratedAssetDependencyManifestPath = dependencyManifestPath;
            hotReloadTask.PreserveEmptyOutputOnRemoval = true;

            hotReloadTask.Execute().ShouldBeTrue();

            hotReloadTask.OutputExists.ShouldBeTrue();
            hotReloadTask.OutputWritten.ShouldBeTrue();
            new FileInfo(outputPath).Length.ShouldBe(0);
            File.Exists(GetManifestPath(outputPath)).ShouldBeFalse();
            File.Exists(GetCatalogPath(outputPath)).ShouldBeFalse();
            var manifestBeforeOrdinaryBuild = File.ReadAllBytes(
                dependencyManifestPath);

            var unchangedHotReloadTask = CreateTask(
                projectDirectory,
                outputPath,
                sourcePath);
            unchangedHotReloadTask.GeneratedAssetDependencyManifestPath =
                dependencyManifestPath;
            unchangedHotReloadTask.PreserveEmptyOutputOnRemoval = true;
            unchangedHotReloadTask.Execute().ShouldBeTrue();
            unchangedHotReloadTask.OutputWritten.ShouldBeFalse();

            var ordinaryTask = CreateTask(projectDirectory, outputPath, sourcePath);
            ordinaryTask.GeneratedAssetDependencyManifestPath =
                dependencyManifestPath;

            ordinaryTask.Execute().ShouldBeTrue();

            ordinaryTask.OutputExists.ShouldBeFalse();
            File.Exists(outputPath).ShouldBeFalse();
            File.Exists(dependencyManifestPath).ShouldBeTrue();
            File.ReadAllBytes(dependencyManifestPath).ShouldBe(
                manifestBeforeOrdinaryBuild);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_NoPriorOutputDuringGeneratedAssetHotReload_DoesNotCreateEmptyOutput()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(sourcePath, "<div>No utility candidate</div>");
            var task = CreateTask(projectDirectory, outputPath, sourcePath);
            task.PreserveEmptyOutputOnRemoval = true;

            task.Execute().ShouldBeTrue();

            task.OutputExists.ShouldBeFalse();
            task.OutputWritten.ShouldBeFalse();
            File.Exists(outputPath).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_EditorSidecarDisabled_DeletesStaleSidecarsAndKeepsBundle()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            File.WriteAllText(sourcePath, "<div class=\"flex\"></div>");
            var firstTask = CreateTask(projectDirectory, outputPath, sourcePath);
            firstTask.Execute().ShouldBeTrue();
            File.Exists(GetManifestPath(outputPath)).ShouldBeTrue();
            File.Exists(GetCatalogPath(outputPath)).ShouldBeTrue();

            var secondTask = CreateTask(projectDirectory, outputPath, sourcePath);
            secondTask.EmitEditorSidecar = false;

            secondTask.Execute().ShouldBeTrue();
            File.Exists(outputPath).ShouldBeTrue();
            File.Exists(GetManifestPath(outputPath)).ShouldBeFalse();
            File.Exists(GetCatalogPath(outputPath)).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void Execute_EditorSidecarEnabled_DeletesPreviousCatalogFilename()
    {
        var projectDirectory = CreateProjectDirectory();
        try
        {
            var sourcePath = Path.Combine(projectDirectory, "index.html");
            var outputPath = Path.Combine(projectDirectory, "obj", "project.utilities.css");
            var previousCatalogPath = Path.Combine(
                Path.GetDirectoryName(outputPath)!,
                "utilitycss.catalog.v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(sourcePath, "<div class=\"flex\"></div>");
            File.WriteAllText(previousCatalogPath, "{}");

            var task = CreateTask(projectDirectory, outputPath, sourcePath);

            task.Execute().ShouldBeTrue();
            File.Exists(previousCatalogPath).ShouldBeFalse();
            File.Exists(GetCatalogPath(outputPath)).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    private static ViuGenerateUtilityCss CreateTask(
        string projectDirectory,
        string outputPath,
        params string[] sourcePaths) =>
        new ViuGenerateUtilityCss
        {
            BuildEngine = new TestBuildEngine(),
            ProjectDirectory = projectDirectory,
            OutputPath = outputPath,
            SourceFiles = Array.ConvertAll(
                sourcePaths,
                sourcePath => (ITaskItem)new TaskItem(sourcePath)),
        };

    private static string CreateProjectDirectory()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-css-build-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);
        return projectDirectory;
    }

    private static string GetManifestPath(string outputPath) =>
        Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            "utilitycss.manifest.v1.json");

    private static string GetCatalogPath(string outputPath) =>
        Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            "utilitycss.classcatalog.v1.json");

    private static GeneratedAssetDependencyManifest ReadGeneratedAssetDependencyManifest(
        string path)
    {
        var lines = File.ReadAllLines(path);
        lines[0].ShouldBe("viu-generated-asset-dependencies-v1");
        return new GeneratedAssetDependencyManifest(
            lines
                .Where(line => line.StartsWith("file:", StringComparison.Ordinal))
                .Select(line => DecodeGeneratedAssetDependencyPath(
                    line.Substring("file:".Length)))
                .ToArray(),
            lines
                .Where(line => line.StartsWith("root:", StringComparison.Ordinal))
                .Select(line => DecodeGeneratedAssetDependencyPath(
                    line.Substring("root:".Length)))
                .ToArray());
    }

    private static string DecodeGeneratedAssetDependencyPath(string encodedPath) =>
        Encoding.UTF8.GetString(
            Convert.FromBase64String(encodedPath));

    private static JsonElement FindCatalogEntry(
        JsonElement entries,
        string candidateText)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            if (string.Equals(
                    entry.GetProperty("class").GetString(),
                    candidateText,
                    StringComparison.Ordinal))
            {
                return entry;
            }
        }

        throw new InvalidOperationException(
            "Catalog entry was not found: " + candidateText);
    }

    private static string WrapMarkup(
        string extension,
        string markup) =>
        extension switch
        {
            ".viu" => $"<template>{markup}</template>",
            ".vue" => $"<template>{markup}</template>",
            _ => markup,
        };

    private sealed class TestBuildEngine : IBuildEngine
    {
        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

        public void LogErrorEvent(BuildErrorEventArgs eventArguments)
        {
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArguments)
        {
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArguments)
        {
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArguments)
        {
        }

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs) =>
            true;
    }

    private sealed record GeneratedAssetDependencyManifest(
        string[] Files,
        string[] Roots);
}
