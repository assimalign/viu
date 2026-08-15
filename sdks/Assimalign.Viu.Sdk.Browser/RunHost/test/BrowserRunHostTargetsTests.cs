using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Sdk.Browser.RunHost.Tests;

public sealed class BrowserRunHostTargetsTests
{
    [Fact]
    public void ComputeRunArguments_BrowserWorkloadDisablesBundle_WrapsFinalCommandOnce()
    {
        using TargetTestProject project = TargetTestProject.Create(enabled: true);

        IReadOnlyList<string> lines = project.Run();

        string configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? string.Empty;
        string expectedCommand = !string.IsNullOrEmpty(configuredHost) && File.Exists(configuredHost)
            ? configuredHost
            : "dotnet";
        lines.ShouldContain($"command={expectedCommand}");
        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" -- \"original-command\" "
            + "exec \"original host.dll\" --flag");
        lines.ShouldContain("working=original-working-directory");
        lines.ShouldContain("configured=true");
    }

    [Fact]
    public void ComputeRunArguments_DeferredRunCommand_WrapsComputedCommandOnce()
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            deferRunCommand: true);

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" -- \"original-command\" "
            + "exec \"original host.dll\" --flag");
        lines.ShouldContain("working=original-working-directory");
        lines.ShouldContain("configured=true");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ComputeRunArguments_DisabledOrAlreadyConfigured_DoesNotWrap(
        bool enabled,
        bool alreadyConfigured)
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled,
            alreadyConfigured);

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain("command=original-command");
        lines.ShouldContain("arguments=exec \"original host.dll\" --flag");
        lines.ShouldContain("working=original-working-directory");
        lines.ShouldContain($"configured={alreadyConfigured.ToString().ToLowerInvariant()}");
    }

    private sealed class TargetTestProject : IDisposable
    {
        private TargetTestProject(
            string directoryPath,
            string projectPath,
            string outputPath,
            string runHostAssemblyPath)
        {
            DirectoryPath = directoryPath;
            ProjectPath = projectPath;
            OutputPath = outputPath;
            RunHostAssemblyPath = runHostAssemblyPath;
        }

        internal string RunHostAssemblyPath { get; }

        private string DirectoryPath { get; }

        private string ProjectPath { get; }

        private string OutputPath { get; }

        internal static TargetTestProject Create(
            bool enabled,
            bool alreadyConfigured = false,
            bool deferRunCommand = false)
        {
            string repositoryDirectory = FindRepositoryDirectory();
            string targetsPath = Path.Combine(
                repositoryDirectory,
                "sdks",
                "Assimalign.Viu.Sdk.Browser",
                "Targets",
                "Assimalign.Viu.Sdk.Browser.WebAssembly.targets");
            string runHostAssemblyPath = typeof(RunHostProcess).Assembly.Location;
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "viu-run-host-targets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            string projectPath = Path.Combine(directoryPath, "Probe.proj");
            string outputPath = Path.Combine(directoryPath, "probe.txt");
            string project =
                "<Project>" + Environment.NewLine
                + "  <PropertyGroup>" + Environment.NewLine
                + "    <_ViuBrowserSdk>true</_ViuBrowserSdk>" + Environment.NewLine
                + "    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>" + Environment.NewLine
                + "    <OutputType>Exe</OutputType>" + Environment.NewLine
                + "    <_IsExecutable>true</_IsExecutable>" + Environment.NewLine
                + "    <WasmGenerateAppBundle>false</WasmGenerateAppBundle>" + Environment.NewLine
                + $"    <ViuBrowserRunHostReadinessEnabled>{enabled.ToString().ToLowerInvariant()}</ViuBrowserRunHostReadinessEnabled>" + Environment.NewLine
                + $"    <_ViuBrowserRunHostConfigured>{alreadyConfigured.ToString().ToLowerInvariant()}</_ViuBrowserRunHostConfigured>" + Environment.NewLine
                + $"    <ViuBrowserRunHostAssembly>{Escape(runHostAssemblyPath)}</ViuBrowserRunHostAssembly>" + Environment.NewLine
                + (deferRunCommand
                    ? string.Empty
                    : "    <RunCommand>original-command</RunCommand>" + Environment.NewLine
                        + "    <RunArguments>exec &quot;original host.dll&quot; --flag</RunArguments>" + Environment.NewLine
                        + "    <RunWorkingDirectory>original-working-directory</RunWorkingDirectory>" + Environment.NewLine)
                + $"    <ProbeOutput>{Escape(outputPath)}</ProbeOutput>" + Environment.NewLine
                + "  </PropertyGroup>" + Environment.NewLine
                + (deferRunCommand
                    ? "  <Target Name=\"ComputeRunArguments\">" + Environment.NewLine
                        + "    <PropertyGroup>" + Environment.NewLine
                        + "      <RunCommand>original-command</RunCommand>" + Environment.NewLine
                        + "      <RunArguments>exec &quot;original host.dll&quot; --flag</RunArguments>" + Environment.NewLine
                        + "      <RunWorkingDirectory>original-working-directory</RunWorkingDirectory>" + Environment.NewLine
                        + "    </PropertyGroup>" + Environment.NewLine
                        + "  </Target>" + Environment.NewLine
                    : "  <Target Name=\"ComputeRunArguments\" />" + Environment.NewLine)
                + $"  <Import Project=\"{Escape(targetsPath)}\" />" + Environment.NewLine
                + "  <Target Name=\"Probe\" DependsOnTargets=\"ComputeRunArguments\">" + Environment.NewLine
                + "    <ItemGroup>" + Environment.NewLine
                + "      <_ProbeLine Include=\"command=$(RunCommand)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"arguments=$(RunArguments)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"working=$(RunWorkingDirectory)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"configured=$(_ViuBrowserRunHostConfigured)\" />" + Environment.NewLine
                + "    </ItemGroup>" + Environment.NewLine
                + "    <WriteLinesToFile File=\"$(ProbeOutput)\" Lines=\"@(_ProbeLine)\" Overwrite=\"true\" Encoding=\"UTF-8\" />" + Environment.NewLine
                + "  </Target>" + Environment.NewLine
                + "</Project>" + Environment.NewLine;
            File.WriteAllText(
                projectPath,
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new TargetTestProject(
                directoryPath,
                projectPath,
                outputPath,
                runHostAssemblyPath);
        }

        internal IReadOnlyList<string> Run()
        {
            ProcessStartInfo startInformation = new()
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[]
            {
                "msbuild",
                ProjectPath,
                "-nologo",
                "-verbosity:quiet",
                "-target:Probe",
            })
            {
                startInformation.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInformation)
                ?? throw new InvalidOperationException("Could not start the MSBuild target probe.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.ShouldBe(
                0,
                $"MSBuild target probe failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
            return File.ReadAllLines(OutputPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static string Escape(string value) =>
            SecurityElement.Escape(value)
            ?? throw new InvalidOperationException("Could not escape an MSBuild probe value.");

        private static string FindRepositoryDirectory()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Assimalign.Viu.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find the Viu repository directory.");
        }
    }
}
