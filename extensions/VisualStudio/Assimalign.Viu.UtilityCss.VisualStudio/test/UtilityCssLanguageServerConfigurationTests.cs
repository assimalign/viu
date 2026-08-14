using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Shouldly;

using Xunit;

using Assimalign.Viu.UtilityCss.VisualStudio;

namespace Assimalign.Viu.UtilityCss.VisualStudio.Tests;

public sealed class UtilityCssLanguageServerConfigurationTests
{
    [Fact]
    public void GetExtensionDirectory_AssemblyLocation_ReturnsAssemblyDirectory()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-utilitycss-visual-studio-{Guid.NewGuid():N}");
        string assemblyLocation = Path.Combine(
            extensionDirectory,
            "Assimalign.Viu.UtilityCss.VisualStudio.dll");

        UtilityCssLanguageServerConfiguration.GetExtensionDirectory(assemblyLocation)
            .ShouldBe(extensionDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetExtensionDirectory_NoAssemblyLocation_FallsBackToCurrentDirectory(
        string assemblyLocation)
    {
        UtilityCssLanguageServerConfiguration.GetExtensionDirectory(assemblyLocation)
            .ShouldBe(Directory.GetCurrentDirectory());
    }

    [Fact]
    public void ResolveExecutablePath_DefaultLayout_SelectsArchitectureSpecificPayload()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-utilitycss-visual-studio-{Guid.NewGuid():N}");
        UtilityCssLanguageServerConfiguration configuration =
            UtilityCssLanguageServerConfiguration.Load(extensionDirectory);

        configuration.ResolveExecutablePath(extensionDirectory, Architecture.X64)
            .ShouldBe(
                Path.Combine(
                    extensionDirectory,
                    "LanguageServer",
                    "win-x64",
                    "Assimalign.Viu.UtilityCss.LanguageServer.exe"));
        configuration.ResolveExecutablePath(extensionDirectory, Architecture.Arm64)
            .ShouldBe(
                Path.Combine(
                    extensionDirectory,
                    "LanguageServer",
                    "win-arm64",
                    "Assimalign.Viu.UtilityCss.LanguageServer.exe"));
    }

    [Fact]
    public void ResolveExecutablePath_PathEscapingExtensionDirectory_Throws()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-utilitycss-visual-studio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extensionDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(extensionDirectory, "language-server.json"),
                """
                {
                  "relativeExecutablePaths": {
                    "x64": "../elsewhere/server.exe",
                    "arm64": "../elsewhere/server.exe"
                  }
                }
                """);
            UtilityCssLanguageServerConfiguration configuration =
                UtilityCssLanguageServerConfiguration.Load(extensionDirectory);

            Should.Throw<InvalidDataException>(
                () => configuration.ResolveExecutablePath(
                    extensionDirectory,
                    Architecture.X64));
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveExecutablePath_UnsupportedArchitecture_Throws()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-utilitycss-visual-studio-{Guid.NewGuid():N}");
        UtilityCssLanguageServerConfiguration configuration =
            UtilityCssLanguageServerConfiguration.Load(extensionDirectory);

        Should.Throw<PlatformNotSupportedException>(
            () => configuration.ResolveExecutablePath(
                extensionDirectory,
                Architecture.X86));
    }

    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "--stdio" }, "--stdio")]
    [InlineData(new[] { "--log", @"C:\Program Files\utilitycss.log" }, @"--log ""C:\Program Files\utilitycss.log""")]
    [InlineData(new[] { @"C:\Program Files\" }, @"""C:\Program Files\\""")]
    public void FormatArguments_ConfiguredArguments_QuotesForCommandLineToArgv(
        string[] arguments,
        string expectedCommandLine)
    {
        UtilityCssLanguageServerConfiguration.FormatArguments(arguments)
            .ShouldBe(expectedCommandLine);
    }

    [Fact]
    public void CreateProcessStartInformation_ConfiguredArguments_RedirectsProtocolAndDiagnosticStreams()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-utilitycss-visual-studio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extensionDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(extensionDirectory, "language-server.json"),
                """
                {
                  "arguments": ["--stdio"]
                }
                """);
            UtilityCssLanguageServerConfiguration configuration =
                UtilityCssLanguageServerConfiguration.Load(extensionDirectory);
            string executablePath = configuration.ResolveExecutablePath(
                extensionDirectory,
                Architecture.Arm64);

            ProcessStartInfo startInformation = configuration.CreateProcessStartInformation(
                executablePath,
                extensionDirectory);

            startInformation.FileName.ShouldBe(executablePath);
            startInformation.Arguments.ShouldBe("--stdio");
            startInformation.WorkingDirectory.ShouldBe(Path.GetDirectoryName(executablePath));
            startInformation.RedirectStandardInput.ShouldBeTrue();
            startInformation.RedirectStandardOutput.ShouldBeTrue();
            startInformation.RedirectStandardError.ShouldBeTrue();
            startInformation.UseShellExecute.ShouldBeFalse();
            startInformation.CreateNoWindow.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }
}
