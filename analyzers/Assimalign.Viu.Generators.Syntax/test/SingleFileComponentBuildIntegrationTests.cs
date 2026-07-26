using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Executes the packaged analyzer targets through real MSBuild evaluation, pinning
/// [V01.01.06.09]'s component discovery and <c>dotnet watch</c> item graph.
/// </summary>
public sealed class SingleFileComponentBuildIntegrationTests
{
    /// <summary>
    /// The packaged props surface both the Configuration default and the explicit
    /// ViuEmitHotReloadMetadata override through Roslyn's analyzer-config bridge.
    /// </summary>
    [Fact]
    public async Task CompilerVisibleProperties_ExposeHotReloadMetadataGates()
    {
        var repositoryDirectory = FindRepositoryDirectory();
        var propsPath = Path.Combine(
            repositoryDirectory,
            "analyzers",
            "Assimalign.Viu.Generators.Syntax",
            "build",
            "Assimalign.Viu.Generators.Syntax.props");
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-component-properties-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "Probe.proj"),
                CreateCompilerVisiblePropertyProbe(propsPath));

            await RunProbeAsync(temporaryDirectory, "Probe.proj", "Probe");

            var properties = File.ReadAllLines(Path.Combine(temporaryDirectory, "properties.txt"));
            properties.ShouldContain("Configuration");
            properties.ShouldContain("ViuEmitHotReloadMetadata");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Re-evaluating the imported targets discovers tag-based component additions, deletions, and
    /// renames, returns the same files through component, AdditionalFiles, and Watch items, and keeps
    /// both files in a same-base collision available for the generator's <c>.viu</c>-wins policy.
    /// </summary>
    [Fact]
    public async Task ComponentItems_VueAddDeleteAndRename_AreDiscoveredAcrossReevaluation()
    {
        var repositoryDirectory = FindRepositoryDirectory();
        var targetsPath = Path.Combine(
            repositoryDirectory,
            "analyzers",
            "Assimalign.Viu.Generators.Syntax",
            "build",
            "Assimalign.Viu.Generators.Syntax.targets");
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-component-items-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "Probe.proj"),
                CreateProbeProject(targetsPath));
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "Transitions.proj"),
                CreateTransitionProject());

            await RunProbeAsync(temporaryDirectory, "Transitions.proj", "Run");

            AssertDiscovered(
                ReadSnapshot(temporaryDirectory, "initial.txt"),
                "Choice.viu",
                "Choice.vue",
                "Existing.vue");
            AssertDiscovered(
                ReadSnapshot(temporaryDirectory, "added.txt"),
                "Added.vue",
                "Choice.viu",
                "Choice.vue",
                "Existing.vue");
            AssertDiscovered(
                ReadSnapshot(temporaryDirectory, "renamed.txt"),
                "Choice.viu",
                "Choice.vue",
                "Existing.vue",
                "Renamed.vue");
            AssertDiscovered(
                ReadSnapshot(temporaryDirectory, "deleted.txt"),
                "Choice.viu",
                "Choice.vue",
                "Renamed.vue");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static string CreateProbeProject(string targetsPath)
    {
        var escapedTargetsPath = SecurityElement.Escape(targetsPath)
            ?? throw new InvalidOperationException("The analyzer targets path could not be escaped.");
        return $$"""
            <Project>
              <PropertyGroup>
                <EnableSingleFileComponentGeneration>true</EnableSingleFileComponentGeneration>
              </PropertyGroup>
              <Import Project="{{escapedTargetsPath}}" />
              <Target Name="Probe" DependsOnTargets="_ViuCollectSingleFileComponentGeneratorWatchItems">
                <ItemGroup>
                  <_ProbeLine Include="@(ViuSingleFileComponent->'Component|%(Filename)%(Extension)')" />
                  <_ProbeLine Include="@(AdditionalFiles->'AdditionalFile|%(Filename)%(Extension)')" />
                  <_ProbeLine Include="@(Watch->'Watch|%(Filename)%(Extension)')" />
                </ItemGroup>
                <WriteLinesToFile
                    File="$(ProbeOutput)"
                    Lines="@(_ProbeLine)"
                    Overwrite="true" />
              </Target>
            </Project>
            """;
    }

    private static string CreateCompilerVisiblePropertyProbe(string propsPath)
    {
        var escapedPropsPath = SecurityElement.Escape(propsPath)
            ?? throw new InvalidOperationException("The analyzer props path could not be escaped.");
        return $$"""
            <Project>
              <PropertyGroup>
                <EnableSingleFileComponentGeneration>true</EnableSingleFileComponentGeneration>
              </PropertyGroup>
              <Import Project="{{escapedPropsPath}}" />
              <Target Name="Probe">
                <WriteLinesToFile
                    File="$(MSBuildProjectDirectory)/properties.txt"
                    Lines="@(CompilerVisibleProperty)"
                    Overwrite="true" />
              </Target>
            </Project>
            """;
    }

    private static string CreateTransitionProject()
        => """
            <Project>
              <PropertyGroup>
                <ProbeProject>$(MSBuildProjectDirectory)/Probe.proj</ProbeProject>
              </PropertyGroup>
              <Target Name="Run">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/Choice.viu" Lines="canonical" Overwrite="true" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/Choice.vue" Lines="compatibility" Overwrite="true" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)/Existing.vue" Lines="existing" Overwrite="true" />
                <MSBuild
                    Projects="$(ProbeProject)"
                    Targets="Probe"
                    Properties="ProbeOutput=$(MSBuildProjectDirectory)/initial.txt;ProbeEvaluation=Initial" />

                <WriteLinesToFile File="$(MSBuildProjectDirectory)/Added.vue" Lines="added" Overwrite="true" />
                <MSBuild
                    Projects="$(ProbeProject)"
                    Targets="Probe"
                    Properties="ProbeOutput=$(MSBuildProjectDirectory)/added.txt;ProbeEvaluation=Added" />

                <Move
                    SourceFiles="$(MSBuildProjectDirectory)/Added.vue"
                    DestinationFiles="$(MSBuildProjectDirectory)/Renamed.vue" />
                <MSBuild
                    Projects="$(ProbeProject)"
                    Targets="Probe"
                    Properties="ProbeOutput=$(MSBuildProjectDirectory)/renamed.txt;ProbeEvaluation=Renamed" />

                <Delete Files="$(MSBuildProjectDirectory)/Existing.vue" />
                <MSBuild
                    Projects="$(ProbeProject)"
                    Targets="Probe"
                    Properties="ProbeOutput=$(MSBuildProjectDirectory)/deleted.txt;ProbeEvaluation=Deleted" />
              </Target>
            </Project>
            """;

    private static async Task RunProbeAsync(
        string projectDirectory,
        string projectFileName,
        string targetName)
    {
        var projectPath = Path.Combine(projectDirectory, projectFileName);
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        processStartInfo.ArgumentList.Add("msbuild");
        processStartInfo.ArgumentList.Add(projectPath);
        processStartInfo.ArgumentList.Add("-nologo");
        processStartInfo.ArgumentList.Add("-verbosity:quiet");
        processStartInfo.ArgumentList.Add("-target:" + targetName);

        using var process = new Process { StartInfo = processStartInfo };
        process.Start().ShouldBeTrue();
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        process.ExitCode.ShouldBe(
            0,
            "MSBuild component-item probe failed.\n" + standardOutput + "\n" + standardError);
    }

    private static string[] ReadSnapshot(string projectDirectory, string fileName)
        => File.ReadAllLines(Path.Combine(projectDirectory, fileName));

    private static void AssertDiscovered(string[] lines, params string[] expectedFileNames)
    {
        AssertCategory(lines, "Component|", expectedFileNames);
        AssertCategory(lines, "AdditionalFile|", expectedFileNames);
        AssertCategory(lines, "Watch|", expectedFileNames);
    }

    private static void AssertCategory(
        string[] lines,
        string category,
        string[] expectedFileNames)
    {
        var actual = lines
            .Where(line => line.StartsWith(category, StringComparison.Ordinal))
            .Select(line => line.Substring(category.Length))
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();
        var expected = expectedFileNames
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected);
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

        throw new DirectoryNotFoundException(
            "Could not locate the Viu repository from the generator test output directory.");
    }
}
