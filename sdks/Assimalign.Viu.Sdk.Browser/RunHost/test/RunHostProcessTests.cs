using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Sdk.Browser.RunHost.Tests;

public sealed class RunHostProcessTests
{
    [Fact]
    public async Task RunAsync_WasmAppHostAddress_ForwardsOutputAndMirrorsReadiness()
    {
        using TestProject project = TestProject.Create(
            "App url: http://127.0.0.1:51235/",
            "ordinary output");
        using StringWriter standardOutput = new(CultureInfo.InvariantCulture);
        using StringWriter standardError = new(CultureInfo.InvariantCulture);

        int exitCode = await RunHostProcess.RunAsync(
            project.CreateArguments(),
            standardOutput,
            standardError);

        exitCode.ShouldBe(0);
        string output = standardOutput.ToString();
        output.ShouldContain("App url: http://127.0.0.1:51235/");
        output.ShouldContain("Now listening on: http://127.0.0.1:51235/");
        output.ShouldContain("ordinary output");
        CountOccurrences(output, "Now listening on:").ShouldBe(1);
        output.IndexOf("App url:", StringComparison.Ordinal).ShouldBeLessThan(
            output.IndexOf("Now listening on:", StringComparison.Ordinal));
        standardError.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_InvalidAddresses_ForwardsWithoutReadinessMarkers()
    {
        using TestProject project = TestProject.Create(
            "App url: not-a-url",
            "App url: ftp://127.0.0.1/file");
        using StringWriter standardOutput = new(CultureInfo.InvariantCulture);
        using StringWriter standardError = new(CultureInfo.InvariantCulture);

        int exitCode = await RunHostProcess.RunAsync(
            project.CreateArguments(),
            standardOutput,
            standardError);

        exitCode.ShouldBe(0);
        standardOutput.ToString().ShouldContain("App url: not-a-url");
        standardOutput.ToString().ShouldContain("App url: ftp://127.0.0.1/file");
        standardOutput.ToString().ShouldNotContain("Now listening on:");
        standardError.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_FailingChild_ReturnsExactExitCodeAndForwardsError()
    {
        using TestProject project = TestProject.CreateFailure("expected child failure");
        using StringWriter standardOutput = new(CultureInfo.InvariantCulture);
        using StringWriter standardError = new(CultureInfo.InvariantCulture);

        int exitCode = await RunHostProcess.RunAsync(
            project.CreateArguments(),
            standardOutput,
            standardError);

        exitCode.ShouldBe(1);
        (standardOutput.ToString() + standardError).ShouldContain("expected child failure");
    }

    [Fact]
    public async Task RunAsync_ChildUsageError_ForwardsStandardErrorAndReturnsExitCodeTwo()
    {
        using StringWriter standardOutput = new(CultureInfo.InvariantCulture);
        using StringWriter standardError = new(CultureInfo.InvariantCulture);
        IReadOnlyList<string> arguments =
        [
            "--",
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            "exec",
            typeof(RunHostProcess).Assembly.Location,
            "--",
        ];

        int exitCode = await RunHostProcess.RunAsync(
            arguments,
            standardOutput,
            standardError);

        exitCode.ShouldBe(2);
        standardOutput.ToString().ShouldBeEmpty();
        standardError.ToString().ShouldContain("Usage:");
    }

    [Fact]
    public async Task RunAsync_MissingCommand_ReturnsUsageError()
    {
        using StringWriter standardOutput = new(CultureInfo.InvariantCulture);
        using StringWriter standardError = new(CultureInfo.InvariantCulture);

        int exitCode = await RunHostProcess.RunAsync(
            ["--"],
            standardOutput,
            standardError);

        exitCode.ShouldBe(2);
        standardOutput.ToString().ShouldBeEmpty();
        standardError.ToString().ShouldContain("Usage:");
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private sealed class TestProject : IDisposable
    {
        private TestProject(string directoryPath, string projectPath)
        {
            DirectoryPath = directoryPath;
            ProjectPath = projectPath;
        }

        private string DirectoryPath { get; }

        private string ProjectPath { get; }

        internal static TestProject Create(params string[] messages) =>
            CreateProject(messages, failure: null);

        internal static TestProject CreateFailure(string failure) =>
            CreateProject([], failure);

        internal IReadOnlyList<string> CreateArguments() =>
        [
            "--",
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            "msbuild",
            ProjectPath,
            "-nologo",
            "-verbosity:minimal",
            "-target:RunHostProbe",
        ];

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static TestProject CreateProject(
            IReadOnlyList<string> messages,
            string? failure)
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "viu-run-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            string projectPath = Path.Combine(directoryPath, "RunHostProbe.proj");
            StringBuilder project = new();
            project.AppendLine("<Project>");
            project.AppendLine("  <Target Name=\"RunHostProbe\">");
            foreach (string message in messages)
            {
                project.Append("    <Message Importance=\"High\" Text=\"")
                    .Append(EscapeAttribute(message))
                    .AppendLine("\" />");
            }

            if (failure is not null)
            {
                project.Append("    <Error Text=\"")
                    .Append(EscapeAttribute(failure))
                    .AppendLine("\" />");
            }

            project.AppendLine("  </Target>");
            project.AppendLine("</Project>");
            File.WriteAllText(
                projectPath,
                project.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new TestProject(directoryPath, projectPath);
        }

        private static string EscapeAttribute(string value) => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
