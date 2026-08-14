using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Build.Tests;

// [V01.01.12.30] pins the standalone task's source routing, SFC slicing, CSS-first entry,
// deterministic-write, and stale-output contracts independently of any Viu SDK.
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
            var expectedTimestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(outputPath, expectedTimestamp);

            var secondTask = CreateTask(projectDirectory, outputPath, sourcePath);

            secondTask.Execute().ShouldBeTrue();
            secondTask.OutputWritten.ShouldBeFalse();
            File.ReadAllBytes(outputPath).ShouldBe(expectedBytes);
            File.GetLastWriteTimeUtc(outputPath).ShouldBe(expectedTimestamp);
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
            File.WriteAllText(sourcePath, "<div>No utility candidate</div>");

            var secondTask = CreateTask(projectDirectory, outputPath, sourcePath);

            secondTask.Execute().ShouldBeTrue();
            secondTask.OutputExists.ShouldBeFalse();
            File.Exists(outputPath).ShouldBeFalse();
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
}
