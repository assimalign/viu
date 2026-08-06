using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

public class ViuLanguageServerConfigurationTests
{
    // The in-process client has no host-supplied installation path: it derives the extension
    // directory from the location of its own assembly, which the VSIX lays out beside
    // language-server.json and the LanguageServer\ payload.
    [Fact]
    public void GetExtensionDirectory_AssemblyLocation_IsTheDirectoryHoldingTheAssembly()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
        string assemblyLocation = Path.Combine(
            extensionDirectory,
            "Assimalign.Viu.VisualStudio.dll");

        ViuLanguageServerConfiguration.GetExtensionDirectory(assemblyLocation)
            .ShouldBe(extensionDirectory);
    }

    // An assembly loaded from bytes reports an empty location. Falling back keeps activation on the
    // File.Exists guard, which reports a path, rather than on an exception with none.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetExtensionDirectory_NoAssemblyLocation_FallsBackToTheCurrentDirectory(
        string assemblyLocation)
    {
        ViuLanguageServerConfiguration.GetExtensionDirectory(assemblyLocation)
            .ShouldBe(Directory.GetCurrentDirectory());
    }

    // Both payloads ship in one VSIX and the client picks by host architecture, so a fake layout
    // must resolve each to the file that is actually there.
    [Fact]
    public void ResolveExecutablePath_PackagedLayout_SelectsThePayloadPresentForEachArchitecture()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
        string x64Directory = Path.Combine(extensionDirectory, "LanguageServer", "win-x64");
        string arm64Directory = Path.Combine(extensionDirectory, "LanguageServer", "win-arm64");
        Directory.CreateDirectory(x64Directory);
        Directory.CreateDirectory(arm64Directory);

        try
        {
            const string ExecutableName = "Assimalign.Viu.LanguageServer.exe";
            File.WriteAllText(Path.Combine(x64Directory, ExecutableName), string.Empty);
            File.WriteAllText(Path.Combine(arm64Directory, ExecutableName), string.Empty);

            ViuLanguageServerConfiguration configuration =
                ViuLanguageServerConfiguration.Load(extensionDirectory);

            File.Exists(configuration.ResolveExecutablePath(extensionDirectory, Architecture.X64))
                .ShouldBeTrue();
            File.Exists(configuration.ResolveExecutablePath(extensionDirectory, Architecture.Arm64))
                .ShouldBeTrue();
            configuration.ResolveExecutablePath(extensionDirectory, Architecture.X64)
                .ShouldBe(Path.Combine(x64Directory, ExecutableName));
            configuration.ResolveExecutablePath(extensionDirectory, Architecture.Arm64)
                .ShouldBe(Path.Combine(arm64Directory, ExecutableName));
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveExecutablePath_PathEscapingTheExtensionDirectory_Throws()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
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

            ViuLanguageServerConfiguration configuration =
                ViuLanguageServerConfiguration.Load(extensionDirectory);

            Should.Throw<InvalidDataException>(
                () => configuration.ResolveExecutablePath(extensionDirectory, Architecture.X64));
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }

    // The .NET Framework surface a classic extension compiles against has no ArgumentList, so the
    // client builds the command line itself under the rules CommandLineToArgvW reverses.
    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "--stdio" }, "--stdio")]
    [InlineData(new[] { "--stdio", "--trace" }, "--stdio --trace")]
    [InlineData(new[] { "--log", @"C:\Program Files\viu.log" }, @"--log ""C:\Program Files\viu.log""")]
    // A backslash is only significant before a quote, so an unquoted argument keeps its run as is.
    [InlineData(new[] { @"C:\payload\" }, @"C:\payload\")]
    // Quoted, the same trailing run would escape the closing quote, so it doubles.
    [InlineData(new[] { @"C:\Program Files\" }, @"""C:\Program Files\\""")]
    [InlineData(new[] { "" }, @"""""")]
    [InlineData(new[] { @"a\""b c" }, @"""a\\\""b c""")]
    public void FormatArguments_ConfiguredArguments_QuotesForCommandLineToArgv(
        string[] arguments,
        string expectedCommandLine)
    {
        ViuLanguageServerConfiguration.FormatArguments(arguments).ShouldBe(expectedCommandLine);
    }

    [Fact]
    public void CreateProcessStartInformation_ConfiguredArguments_RedirectsTheProtocolStreamsOnly()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
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

            ViuLanguageServerConfiguration configuration =
                ViuLanguageServerConfiguration.Load(extensionDirectory);
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
            // Standard error stays inherited: the server reserves it for diagnostics, and a
            // redirected pipe nobody drains blocks the server once its buffer fills.
            startInformation.RedirectStandardError.ShouldBeFalse();
            startInformation.UseShellExecute.ShouldBeFalse();
            startInformation.CreateNoWindow.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveExecutablePath_DefaultConfiguration_SelectsArchitectureSpecificPayload()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
        ViuLanguageServerConfiguration configuration =
            ViuLanguageServerConfiguration.Load(extensionDirectory);

        string x64ExecutablePath = configuration.ResolveExecutablePath(
            extensionDirectory,
            Architecture.X64);
        string arm64ExecutablePath = configuration.ResolveExecutablePath(
            extensionDirectory,
            Architecture.Arm64);

        x64ExecutablePath.ShouldBe(
            Path.Combine(
                extensionDirectory,
                "LanguageServer",
                "win-x64",
                "Assimalign.Viu.LanguageServer.exe"));
        arm64ExecutablePath.ShouldBe(
            Path.Combine(
                extensionDirectory,
                "LanguageServer",
                "win-arm64",
                "Assimalign.Viu.LanguageServer.exe"));
    }

    [Fact]
    public void ResolveExecutablePath_UnsupportedArchitecture_Throws()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
        ViuLanguageServerConfiguration configuration =
            ViuLanguageServerConfiguration.Load(extensionDirectory);

        Should.Throw<PlatformNotSupportedException>(
            () => configuration.ResolveExecutablePath(
                extensionDirectory,
                Architecture.X86));
    }

    [Fact]
    public void Load_ArchitectureSpecificConfiguration_UsesConfiguredPaths()
    {
        string extensionDirectory = Path.Combine(
            Path.GetTempPath(),
            $"viu-visual-studio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extensionDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(extensionDirectory, "language-server.json"),
                """
                {
                  "relativeExecutablePaths": {
                    "x64": "servers/x64/server.exe",
                    "arm64": "servers/arm64/server.exe"
                  },
                  "arguments": ["--stdio"]
                }
                """);

            ViuLanguageServerConfiguration configuration =
                ViuLanguageServerConfiguration.Load(extensionDirectory);

            configuration.ResolveExecutablePath(extensionDirectory, Architecture.X64)
                .ShouldBe(Path.Combine(extensionDirectory, "servers", "x64", "server.exe"));
            configuration.ResolveExecutablePath(extensionDirectory, Architecture.Arm64)
                .ShouldBe(Path.Combine(extensionDirectory, "servers", "arm64", "server.exe"));
            configuration.Arguments.ShouldBe(["--stdio"]);
        }
        finally
        {
            Directory.Delete(extensionDirectory, recursive: true);
        }
    }
}
