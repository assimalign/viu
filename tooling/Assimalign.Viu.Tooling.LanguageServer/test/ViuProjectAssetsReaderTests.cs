using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// Pins the pure <c>obj\project.assets.json</c> resolution ([V01.01.12.23], #259) over temporary
/// checked-in-style fixtures: package-folder resolution order with the <c>.nupkg.metadata</c>
/// restore marker, placeholder (<c>_._</c>) skipping, targeting-pack and downloadDependencies
/// framework references, the project-reference built-output probe, the source cone, the
/// editorconfig name channel with its fallbacks, and the degradation statuses.
/// </summary>
public class ViuProjectAssetsReaderTests
{
    [Fact]
    public void Read_RestoredPackageAndFrameworkReferences_ResolvesReferencePaths()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            CreateFile(Path.Combine(projectDirectory, "Helper.cs"), "// helper");
            CreateFile(Path.Combine(projectDirectory, "Component.viu"), "@script\n");
            CreateFile(Path.Combine(projectDirectory, "bin", "Excluded.cs"), "// output");
            CreateFile(Path.Combine(projectDirectory, "obj", "Generated.cs"), "// output");

            // Two package folders in declared order: the decoy holds the assembly but not the
            // restore-success marker, so the second folder must win.
            var decoyFolder = Path.Combine(root, "decoy");
            var packagesFolder = Path.Combine(root, "packages");
            CreateFile(
                Path.Combine(decoyFolder, "fake.package", "1.0.0", "lib", "net10.0", "Fake.Package.dll"));
            CreateFile(
                Path.Combine(packagesFolder, "fake.package", "1.0.0", ".nupkg.metadata"));
            var packageAssembly = Path.Combine(
                packagesFolder, "fake.package", "1.0.0", "lib", "net10.0", "Fake.Package.dll");
            CreateFile(packageAssembly);
            var frameworkPackAssembly = Path.Combine(
                packagesFolder, "fake.app.ref", "2.0.0", "ref", "net10.0", "Fake.App.dll");
            CreateFile(frameworkPackAssembly);

            // A fake dotnet root: the stable 10.0 pack must beat the higher prerelease, and the
            // 11.0 pack must be ignored entirely.
            var dotnetRoot = Path.Combine(root, "dotnet");
            var runtimeIdentifierGraphPath = Path.Combine(
                dotnetRoot, "sdk", "10.0.100", "PortableRuntimeIdentifierGraph.json");
            CreateFile(runtimeIdentifierGraphPath);
            var netCoreAssembly = Path.Combine(
                dotnetRoot, "packs", "Microsoft.NETCore.App.Ref", "10.0.9", "ref", "net10.0",
                "System.Runtime.dll");
            CreateFile(netCoreAssembly);
            CreateFile(
                Path.Combine(
                    dotnetRoot, "packs", "Microsoft.NETCore.App.Ref", "10.0.10-preview.7", "ref",
                    "net10.0", "Prerelease.dll"));
            CreateFile(
                Path.Combine(
                    dotnetRoot, "packs", "Microsoft.NETCore.App.Ref", "11.0.0", "ref", "net11.0",
                    "Wrong.dll"));

            CreateFile(
                Path.Combine(
                    projectDirectory, "obj", "Debug", "net10.0",
                    "Application.GeneratedMSBuildEditorConfig.editorconfig"),
                $"""
                 is_global = true
                 build_property.RootNamespace = First.Value
                 build_property.RootNamespace = Fixture.Namespace
                 build_property.ProjectDir = {projectDirectory}{Path.DirectorySeparatorChar}
                 build_property.Configuration = Debug
                 """);
            WriteAssetsFile(
                projectDirectory,
                targets: new Dictionary<string, object?>
                {
                    ["Fake.Package/1.0.0"] = new Dictionary<string, object?>
                    {
                        ["type"] = "package",
                        ["compile"] = new Dictionary<string, object?>
                        {
                            ["lib/net10.0/Fake.Package.dll"] = new Dictionary<string, object?>(),
                        },
                    },
                    ["Placeholder.Package/2.0.0"] = new Dictionary<string, object?>
                    {
                        ["type"] = "package",
                        ["compile"] = new Dictionary<string, object?>
                        {
                            ["ref/netstandard2.0/_._"] = new Dictionary<string, object?>(),
                        },
                    },
                },
                libraries: new Dictionary<string, object?>
                {
                    ["Fake.Package/1.0.0"] = new Dictionary<string, object?>
                    {
                        ["type"] = "package",
                        ["path"] = "fake.package/1.0.0",
                    },
                    ["Placeholder.Package/2.0.0"] = new Dictionary<string, object?>
                    {
                        ["type"] = "package",
                        ["path"] = "placeholder.package/2.0.0",
                    },
                },
                packageFolders: [decoyFolder, packagesFolder],
                framework: new Dictionary<string, object?>
                {
                    ["frameworkReferences"] = new Dictionary<string, object?>
                    {
                        ["Fake.App"] = new Dictionary<string, object?>(),
                        ["Microsoft.NETCore.App"] = new Dictionary<string, object?>(),
                    },
                    ["downloadDependencies"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["name"] = "Fake.App.Ref",
                            ["version"] = "[2.0.0, 2.0.0]",
                        },
                    },
                    ["runtimeIdentifierGraphPath"] = runtimeIdentifierGraphPath,
                });
            MakeProjectFileOlderThanAssets(projectFilePath);

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.SemanticsActive);
            assets.Status.Detail.ShouldBeNull();
            assets.TargetFramework.ShouldBe("net10.0");
            assets.ReferenceAssemblyPaths.ShouldContain(packageAssembly);
            assets.ReferenceAssemblyPaths.ShouldContain(frameworkPackAssembly);
            assets.ReferenceAssemblyPaths.ShouldContain(netCoreAssembly);
            assets.ReferenceAssemblyPaths.ShouldAllBe(
                path => !path.Contains("decoy") && !path.Contains("_._"));
            assets.ReferenceAssemblyPaths.ShouldAllBe(
                path => !path.Contains("Prerelease.dll") && !path.Contains("Wrong.dll"));
            assets.SourceFilePaths.ShouldBe(new[] { Path.Combine(projectDirectory, "Helper.cs") });
            assets.ComponentFilePaths.ShouldBe(new[] { Path.Combine(projectDirectory, "Component.viu") });
            assets.RootNamespace.ShouldBe("Fixture.Namespace");
            assets.ProjectDirectory.ShouldBe(projectDirectory + Path.DirectorySeparatorChar);
            assets.Configuration.ShouldBe("Debug");
            assets.CacheStamp.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_PackageRestoredInNoFolder_ReportsUnresolvableWithRestoreRemedy()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");

            // The assembly exists but no folder carries the .nupkg.metadata restore marker.
            var packagesFolder = Path.Combine(root, "packages");
            CreateFile(
                Path.Combine(packagesFolder, "fake.package", "1.0.0", "lib", "net10.0", "Fake.Package.dll"));
            WriteAssetsFile(
                projectDirectory,
                targets: new Dictionary<string, object?>
                {
                    ["Fake.Package/1.0.0"] = new Dictionary<string, object?>
                    {
                        ["type"] = "package",
                        ["compile"] = new Dictionary<string, object?>
                        {
                            ["lib/net10.0/Fake.Package.dll"] = new Dictionary<string, object?>(),
                        },
                    },
                },
                libraries: new Dictionary<string, object?>
                {
                    ["Fake.Package/1.0.0"] = new Dictionary<string, object?>
                    {
                        ["type"] = "package",
                        ["path"] = "fake.package/1.0.0",
                    },
                },
                packageFolders: [packagesFolder],
                framework: new Dictionary<string, object?>());

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.AssetsUnresolvable);
            assets.Status.Detail.ShouldNotBeNull();
            assets.Status.Detail!.ShouldContain("Fake.Package/1.0.0");
            assets.Status.Detail!.ShouldContain("run dotnet restore");
            assets.ReferenceAssemblyPaths.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_MissingTargetingPackDirectory_ReportsInstallSdkRemedy()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");

            // The pinned dotnet root exists but carries no Microsoft.NETCore.App.Ref pack.
            var dotnetRoot = Path.Combine(root, "dotnet");
            var runtimeIdentifierGraphPath = Path.Combine(
                dotnetRoot, "sdk", "10.0.100", "PortableRuntimeIdentifierGraph.json");
            CreateFile(runtimeIdentifierGraphPath);
            WriteAssetsFile(
                projectDirectory,
                targets: new Dictionary<string, object?>(),
                libraries: new Dictionary<string, object?>(),
                packageFolders: [],
                framework: new Dictionary<string, object?>
                {
                    ["frameworkReferences"] = new Dictionary<string, object?>
                    {
                        ["Microsoft.NETCore.App"] = new Dictionary<string, object?>(),
                    },
                    ["runtimeIdentifierGraphPath"] = runtimeIdentifierGraphPath,
                });

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.AssetsUnresolvable);
            assets.Status.Detail.ShouldNotBeNull();
            assets.Status.Detail!.ShouldContain("Microsoft.NETCore.App.Ref");
            assets.Status.Detail!.ShouldContain("install the .NET SDK 10");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ProjectReferenceWithBuiltOutput_ReferencesNewestOutput()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");

            var referencedDirectory = Path.Combine(root, "Referenced.Library");
            var debugOutput = Path.Combine(
                referencedDirectory, "bin", "Debug", "net10.0", "Referenced.Library.dll");
            var releaseOutput = Path.Combine(
                referencedDirectory, "bin", "Release", "net10.0", "Referenced.Library.dll");
            CreateFile(debugOutput);
            CreateFile(releaseOutput);
            File.SetLastWriteTimeUtc(debugOutput, DateTime.UtcNow.AddHours(-2));
            File.SetLastWriteTimeUtc(releaseOutput, DateTime.UtcNow.AddHours(-1));
            WriteProjectReferenceAssetsFile(projectDirectory);
            MakeProjectFileOlderThanAssets(projectFilePath);

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.SemanticsActive);
            assets.Status.Detail.ShouldBeNull();
            assets.ReferenceAssemblyPaths.ShouldBe(new[] { releaseOutput });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ProjectReferenceWithoutBuiltOutput_DropsReferenceAndStaysActive()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            Directory.CreateDirectory(Path.Combine(root, "Referenced.Library"));
            WriteProjectReferenceAssetsFile(projectDirectory);
            MakeProjectFileOlderThanAssets(projectFilePath);

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            // A missing project-reference output only drops that reference; semantics stay active
            // and the detail names the remedy.
            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.SemanticsActive);
            assets.Status.Detail.ShouldNotBeNull();
            assets.Status.Detail!.ShouldContain("build Referenced.Library once");
            assets.ReferenceAssemblyPaths.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_MissingAssetsFile_ReportsAssetsMissing()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectFilePath = Path.Combine(root, "Application", "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.AssetsMissing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ProjectFileNewerThanAssets_ReportsStaleAndStillResolves()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            WriteAssetsFile(
                projectDirectory,
                targets: new Dictionary<string, object?>(),
                libraries: new Dictionary<string, object?>(),
                packageFolders: [],
                framework: new Dictionary<string, object?>());
            File.SetLastWriteTimeUtc(projectFilePath, DateTime.UtcNow.AddMinutes(10));

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.Status.Kind.ShouldBe(ViuProjectContextStatusKind.AssetsStale);
            assets.Status.Detail.ShouldNotBeNull();
            assets.Status.Detail!.ShouldContain("run dotnet restore");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_WithoutGeneratedEditorConfig_FallsBackToLiteralRootNamespace()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(
                projectFilePath,
                """
                <Project Sdk="Assimalign.Viu.Sdk">
                  <PropertyGroup>
                    <RootNamespace>$(EvaluatedNamespace)</RootNamespace>
                    <RootNamespace>Custom.Namespace</RootNamespace>
                  </PropertyGroup>
                </Project>
                """);
            WriteAssetsFile(
                projectDirectory,
                targets: new Dictionary<string, object?>(),
                libraries: new Dictionary<string, object?>(),
                packageFolders: [],
                framework: new Dictionary<string, object?>());
            MakeProjectFileOlderThanAssets(projectFilePath);

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            // The non-evaluating fallback: the last literal assignment wins and unresolved
            // $(...) syntax never leaks into the namespace.
            assets.RootNamespace.ShouldBe("Custom.Namespace");
            assets.ProjectDirectory.ShouldBe(projectDirectory + Path.DirectorySeparatorChar);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_WithoutAnyRootNamespaceSource_FallsBackToProjectFileName()
    {
        var root = CreateFixtureRoot();
        try
        {
            var projectDirectory = Path.Combine(root, "Application");
            var projectFilePath = Path.Combine(projectDirectory, "Application.csproj");
            CreateFile(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            WriteAssetsFile(
                projectDirectory,
                targets: new Dictionary<string, object?>(),
                libraries: new Dictionary<string, object?>(),
                packageFolders: [],
                framework: new Dictionary<string, object?>());
            MakeProjectFileOlderThanAssets(projectFilePath);

            var assets = ViuProjectAssetsReader.Read(projectFilePath);

            assets.RootNamespace.ShouldBe("Application");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveDotnetRoot_ProbeOrder_PrefersPinnedRootThenEnvironmentThenProgramFiles()
    {
        var root = CreateFixtureRoot();
        try
        {
            var pinnedRoot = Path.Combine(root, "pinned");
            var environmentRoot = Path.Combine(root, "environment");
            var programFilesRoot = Path.Combine(root, "programfiles");
            Directory.CreateDirectory(Path.Combine(pinnedRoot, "sdk", "10.0.100"));
            Directory.CreateDirectory(environmentRoot);
            Directory.CreateDirectory(Path.Combine(programFilesRoot, "dotnet"));
            var runtimeIdentifierGraphPath = Path.Combine(
                pinnedRoot, "sdk", "10.0.100", "PortableRuntimeIdentifierGraph.json");
            var architectureVariableName =
                "DOTNET_ROOT_" +
                RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [architectureVariableName] = environmentRoot,
            };
            string? GetEnvironmentVariable(string name)
                => environment.TryGetValue(name, out var value) ? value : null;

            // (a) the restore-pinned installation wins over everything.
            ViuProjectAssetsReader
                .ResolveDotnetRoot(runtimeIdentifierGraphPath, GetEnvironmentVariable, programFilesRoot)
                .ShouldBe(pinnedRoot);

            // (b) without the pin, the architecture-specific environment root wins.
            ViuProjectAssetsReader
                .ResolveDotnetRoot(null, GetEnvironmentVariable, programFilesRoot)
                .ShouldBe(environmentRoot);

            // (b) falls through to DOTNET_ROOT when the architecture variable is unset.
            environment.Remove(architectureVariableName);
            environment["DOTNET_ROOT"] = environmentRoot;
            ViuProjectAssetsReader
                .ResolveDotnetRoot(null, GetEnvironmentVariable, programFilesRoot)
                .ShouldBe(environmentRoot);

            // (c) %ProgramFiles%\dotnet is the last resort.
            environment.Clear();
            ViuProjectAssetsReader
                .ResolveDotnetRoot(null, GetEnvironmentVariable, programFilesRoot)
                .ShouldBe(Path.Combine(programFilesRoot, "dotnet"));

            // No probe succeeds: null, which callers report as a missing .NET SDK.
            ViuProjectAssetsReader
                .ResolveDotnetRoot(null, GetEnvironmentVariable, Path.Combine(root, "absent"))
                .ShouldBeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateFixtureRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "viu-project-assets-reader-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CreateFile(string path, string content = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void MakeProjectFileOlderThanAssets(string projectFilePath)
        => File.SetLastWriteTimeUtc(projectFilePath, DateTime.UtcNow.AddMinutes(-10));

    private static void WriteProjectReferenceAssetsFile(string projectDirectory)
        => WriteAssetsFile(
            projectDirectory,
            targets: new Dictionary<string, object?>
            {
                ["Referenced.Library/1.0.0"] = new Dictionary<string, object?>
                {
                    ["type"] = "project",
                    ["compile"] = new Dictionary<string, object?>
                    {
                        ["bin/placeholder/Referenced.Library.dll"] = new Dictionary<string, object?>(),
                    },
                },
            },
            libraries: new Dictionary<string, object?>
            {
                ["Referenced.Library/1.0.0"] = new Dictionary<string, object?>
                {
                    ["type"] = "project",
                    ["path"] = "../Referenced.Library/Referenced.Library.csproj",
                    ["msbuildProject"] = "../Referenced.Library/Referenced.Library.csproj",
                },
            },
            packageFolders: [],
            framework: new Dictionary<string, object?>());

    private static void WriteAssetsFile(
        string projectDirectory,
        Dictionary<string, object?> targets,
        Dictionary<string, object?> libraries,
        string[] packageFolders,
        Dictionary<string, object?> framework)
    {
        var assets = new Dictionary<string, object?>
        {
            ["version"] = 4,
            ["targets"] = new Dictionary<string, object?>
            {
                ["net10.0"] = targets,
            },
            ["libraries"] = libraries,
            ["packageFolders"] = packageFolders.ToDictionary(
                folder => folder,
                _ => (object?)new Dictionary<string, object?>()),
            ["project"] = new Dictionary<string, object?>
            {
                ["version"] = "1.0.0",
                ["frameworks"] = new Dictionary<string, object?>
                {
                    ["net10.0"] = framework,
                },
            },
        };
        var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        CreateFile(assetsFilePath, JsonSerializer.Serialize(assets));
    }
}
