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
        lines.ShouldContain("development-server=WasmAppHost");
        lines.ShouldContain("readiness-prefix=App url:");
        lines.ShouldContain("user-run-parameters=");
    }

    [Fact]
    public void Evaluation_CustomDevelopmentServer_ReplacesWrappedCommand()
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            browserDevelopmentServer: "Custom",
            browserDevelopmentServerCommand: "custom-command",
            browserDevelopmentServerArguments: "serve --flag");

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" -- \"custom-command\" "
            + "serve --flag");
        lines.ShouldContain("working=original-working-directory");
        lines.ShouldContain("configured=true");
        lines.ShouldContain("development-server=Custom");
        lines.ShouldContain("user-run-parameters=true");
        string.Join(Environment.NewLine, lines).ShouldNotContain("--readiness-prefix");
    }

    [Fact]
    public void ComputeRunArguments_CustomDevelopmentServer_ReplacesWrappedCommand()
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            browserDevelopmentServer: "Custom",
            browserDevelopmentServerCommand: "custom-command",
            browserDevelopmentServerArguments: "serve --flag",
            deferDevelopmentServer: true);

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" -- \"custom-command\" "
            + "serve --flag");
        lines.ShouldContain("working=original-working-directory");
        lines.ShouldContain("configured=true");
        lines.ShouldContain("development-server=Custom");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomDevelopmentServer_MissingCommand_FailsBuild(
        bool deferDevelopmentServer)
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            browserDevelopmentServer: "Custom",
            browserDevelopmentServerArguments: "serve --flag",
            deferDevelopmentServer: deferDevelopmentServer);

        string error = project.RunExpectingFailure();

        error.ShouldContain(
            "ViuBrowserDevServerCommand must be set when "
            + "ViuBrowserDevServer is 'Custom'.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DevelopmentServer_UnknownValue_FailsBuild(
        bool deferDevelopmentServer)
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            browserDevelopmentServer: "Unknown",
            deferDevelopmentServer: deferDevelopmentServer);

        string error = project.RunExpectingFailure();

        error.ShouldContain(
            "The Viu Browser dev server 'Unknown' is not supported. "
            + "Set ViuBrowserDevServer to 'WasmAppHost' or 'Custom'.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomDevelopmentServer_NondefaultReadinessPrefix_InjectsRunHostArgument(
        bool deferDevelopmentServer)
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            browserDevelopmentServer: "Custom",
            browserDevelopmentServerCommand: "custom-command",
            browserDevelopmentServerArguments: "serve --flag",
            browserDevelopmentServerReadinessPrefix: "Server ready:",
            deferDevelopmentServer: deferDevelopmentServer);

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" "
            + "--readiness-prefix \"Server ready:\" -- \"custom-command\" "
            + "serve --flag");
    }

    [Fact]
    public void WasmAppHost_NondefaultReadinessPrefix_DoesNotInjectRunHostArgument()
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            browserDevelopmentServerReadinessPrefix: "Server ready:");

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" -- \"original-command\" "
            + "exec \"original host.dll\" --flag");
        string.Join(Environment.NewLine, lines).ShouldNotContain("--readiness-prefix");
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
    [InlineData("Debug", true)]
    [InlineData("Release", false)]
    public void ComputeRunArguments_BuildConfiguration_InjectsWorkerArgumentsOnlyForDebug(
        string configuration,
        bool expectsWorkerArguments)
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            configuration: configuration);

        IReadOnlyList<string> lines = project.Run();

        string workerArguments = expectsWorkerArguments
            ? "--generated-asset-worker-assembly \"worker-assembly.dll\" "
                + "--generated-asset-worker-configuration \"worker.configuration\" "
                + "--generated-asset-worker-state \"worker.state\" "
            : string.Empty;
        lines.ShouldContain(
            $"arguments=exec \"{project.RunHostAssemblyPath}\" {workerArguments}-- "
            + "\"original-command\" exec \"original host.dll\" --flag");
    }

    [Fact]
    public void CommonProps_RelativeIntermediatePath_AnchorsWorkerFilesToProjectDirectory()
    {
        using TargetTestProject project = TargetTestProject.Create(
            enabled: true,
            useDefaultWorkerPaths: true);

        IReadOnlyList<string> lines = project.Run();

        lines.ShouldContain($"state={project.ExpectedStateFilePath}");
        lines.ShouldContain($"configuration={project.ExpectedConfigurationFilePath}");
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

        internal string ExpectedConfigurationFilePath =>
            ExpectedStateFilePath + ".configuration";

        internal string ExpectedStateFilePath => Path.Combine(
            DirectoryPath,
            "obj",
            "viu",
            "css-hot-reload",
            "worker.state");

        private string DirectoryPath { get; }

        private string ProjectPath { get; }

        private string OutputPath { get; }

        internal static TargetTestProject Create(
            bool enabled,
            bool alreadyConfigured = false,
            bool deferRunCommand = false,
            string configuration = "",
            bool useDefaultWorkerPaths = false,
            string browserDevelopmentServer = "",
            string browserDevelopmentServerCommand = "",
            string browserDevelopmentServerArguments = "",
            string browserDevelopmentServerReadinessPrefix = "",
            bool deferDevelopmentServer = false)
        {
            string repositoryDirectory = FindRepositoryDirectory();
            string targetsPath = Path.Combine(
                repositoryDirectory,
                "sdks",
                "Assimalign.Viu.Sdk.Browser",
                "Targets",
                "Assimalign.Viu.Sdk.Browser.WebAssembly.targets");
            string commonPropsPath = Path.Combine(
                repositoryDirectory,
                "sdks",
                "Assimalign.Viu.Sdk.Browser",
                "Targets",
                "Assimalign.Viu.Sdk.Browser.Common.props");
            string runHostAssemblyPath = typeof(RunHostProcess).Assembly.Location;
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "viu-run-host-targets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            string projectPath = Path.Combine(directoryPath, "Probe.proj");
            string outputPath = Path.Combine(directoryPath, "probe.txt");
            string initialDevelopmentServerProperties = deferDevelopmentServer
                ? string.Empty
                : CreateDevelopmentServerProperties(
                    "    ",
                    browserDevelopmentServer,
                    browserDevelopmentServerCommand,
                    browserDevelopmentServerArguments,
                    browserDevelopmentServerReadinessPrefix);
            string computedDevelopmentServerProperties = deferDevelopmentServer
                ? CreateDevelopmentServerProperties(
                    "      ",
                    browserDevelopmentServer,
                    browserDevelopmentServerCommand,
                    browserDevelopmentServerArguments,
                    browserDevelopmentServerReadinessPrefix)
                : string.Empty;
            bool deferRunConfiguration = deferRunCommand || deferDevelopmentServer;
            string project =
                "<Project>" + Environment.NewLine
                + "  <PropertyGroup>" + Environment.NewLine
                + "    <_ViuBrowserSdk>true</_ViuBrowserSdk>" + Environment.NewLine
                + "    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>" + Environment.NewLine
                + "    <OutputType>Exe</OutputType>" + Environment.NewLine
                + "    <_IsExecutable>true</_IsExecutable>" + Environment.NewLine
                + "    <WasmGenerateAppBundle>false</WasmGenerateAppBundle>" + Environment.NewLine
                + "    <BaseIntermediateOutputPath>obj\\</BaseIntermediateOutputPath>" + Environment.NewLine
                + $"    <Configuration>{Escape(configuration)}</Configuration>" + Environment.NewLine
                + "    <ViuCssHotReloadEnabled>true</ViuCssHotReloadEnabled>" + Environment.NewLine
                + (useDefaultWorkerPaths
                    ? string.Empty
                    : "    <ViuCssHotReloadWorkerAssembly>worker-assembly.dll</ViuCssHotReloadWorkerAssembly>" + Environment.NewLine
                        + "    <ViuGeneratedAssetWorkerConfigurationFile>worker.configuration</ViuGeneratedAssetWorkerConfigurationFile>" + Environment.NewLine
                        + "    <ViuCssHotReloadStateFile>worker.state</ViuCssHotReloadStateFile>" + Environment.NewLine)
                + $"    <ViuBrowserRunHostReadinessEnabled>{enabled.ToString().ToLowerInvariant()}</ViuBrowserRunHostReadinessEnabled>" + Environment.NewLine
                + $"    <_ViuBrowserRunHostConfigured>{alreadyConfigured.ToString().ToLowerInvariant()}</_ViuBrowserRunHostConfigured>" + Environment.NewLine
                + $"    <ViuBrowserRunHostAssembly>{Escape(runHostAssemblyPath)}</ViuBrowserRunHostAssembly>" + Environment.NewLine
                + initialDevelopmentServerProperties
                + (deferRunConfiguration
                    ? string.Empty
                    : "    <RunCommand>original-command</RunCommand>" + Environment.NewLine
                        + "    <RunArguments>exec &quot;original host.dll&quot; --flag</RunArguments>" + Environment.NewLine
                        + "    <RunWorkingDirectory>original-working-directory</RunWorkingDirectory>" + Environment.NewLine)
                + $"    <ProbeOutput>{Escape(outputPath)}</ProbeOutput>" + Environment.NewLine
                + "  </PropertyGroup>" + Environment.NewLine
                + (deferRunConfiguration
                    ? "  <Target Name=\"ComputeRunArguments\">" + Environment.NewLine
                        + "    <PropertyGroup>" + Environment.NewLine
                        + computedDevelopmentServerProperties
                        + "      <RunCommand>original-command</RunCommand>" + Environment.NewLine
                        + "      <RunArguments>exec &quot;original host.dll&quot; --flag</RunArguments>" + Environment.NewLine
                        + "      <RunWorkingDirectory>original-working-directory</RunWorkingDirectory>" + Environment.NewLine
                        + "    </PropertyGroup>" + Environment.NewLine
                        + "  </Target>" + Environment.NewLine
                    : "  <Target Name=\"ComputeRunArguments\" />" + Environment.NewLine)
                + $"  <Import Project=\"{Escape(commonPropsPath)}\" />" + Environment.NewLine
                + $"  <Import Project=\"{Escape(targetsPath)}\" />" + Environment.NewLine
                + "  <Target Name=\"Probe\" DependsOnTargets=\"ComputeRunArguments\">" + Environment.NewLine
                + "    <ItemGroup>" + Environment.NewLine
                + "      <_ProbeLine Include=\"command=$(RunCommand)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"arguments=$(RunArguments)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"working=$(RunWorkingDirectory)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"configured=$(_ViuBrowserRunHostConfigured)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"development-server=$(ViuBrowserDevServer)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"readiness-prefix=$(ViuBrowserDevServerReadinessPrefix)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"user-run-parameters=$(_WebAssemblyUserRunParameters)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"state=$(ViuCssHotReloadStateFile)\" />" + Environment.NewLine
                + "      <_ProbeLine Include=\"configuration=$(ViuGeneratedAssetWorkerConfigurationFile)\" />" + Environment.NewLine
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
            (int exitCode, string standardOutput, string standardError) = Execute();
            exitCode.ShouldBe(
                0,
                $"MSBuild target probe failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
            return File.ReadAllLines(OutputPath);
        }

        internal string RunExpectingFailure()
        {
            (int exitCode, string standardOutput, string standardError) = Execute();
            exitCode.ShouldNotBe(
                0,
                $"MSBuild target probe unexpectedly succeeded.{Environment.NewLine}{standardOutput}");
            return standardOutput + Environment.NewLine + standardError;
        }

        private (int ExitCode, string StandardOutput, string StandardError) Execute()
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
            return (process.ExitCode, standardOutput, standardError);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static string CreateDevelopmentServerProperties(
            string indentation,
            string browserDevelopmentServer,
            string browserDevelopmentServerCommand,
            string browserDevelopmentServerArguments,
            string browserDevelopmentServerReadinessPrefix)
        {
            StringBuilder properties = new();
            if (!string.IsNullOrEmpty(browserDevelopmentServer))
            {
                properties.Append(indentation)
                    .Append("<ViuBrowserDevServer>")
                    .Append(Escape(browserDevelopmentServer))
                    .Append("</ViuBrowserDevServer>")
                    .AppendLine();
            }

            if (!string.IsNullOrEmpty(browserDevelopmentServerCommand))
            {
                properties.Append(indentation)
                    .Append("<ViuBrowserDevServerCommand>")
                    .Append(Escape(browserDevelopmentServerCommand))
                    .Append("</ViuBrowserDevServerCommand>")
                    .AppendLine();
            }

            if (!string.IsNullOrEmpty(browserDevelopmentServerArguments))
            {
                properties.Append(indentation)
                    .Append("<ViuBrowserDevServerArguments>")
                    .Append(Escape(browserDevelopmentServerArguments))
                    .Append("</ViuBrowserDevServerArguments>")
                    .AppendLine();
            }

            if (!string.IsNullOrEmpty(browserDevelopmentServerReadinessPrefix))
            {
                properties.Append(indentation)
                    .Append("<ViuBrowserDevServerReadinessPrefix>")
                    .Append(Escape(browserDevelopmentServerReadinessPrefix))
                    .Append("</ViuBrowserDevServerReadinessPrefix>")
                    .AppendLine();
            }

            return properties.ToString();
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
