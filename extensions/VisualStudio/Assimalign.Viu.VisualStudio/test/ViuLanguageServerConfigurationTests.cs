using System;
using System.IO;
using System.Runtime.InteropServices;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

public class ViuLanguageServerConfigurationTests
{
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
