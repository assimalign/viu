using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Scaffolds the on-disk projects the degradation-protocol tests exercise
/// ([V01.01.12.23], #259). The project-state artifact format (today: a real-shaped
/// <c>obj\project.assets.json</c>) is written only here, so the reference-ingestion decision
/// changes exactly one place if a later increment moves to an SDK-emitted references manifest.
/// </summary>
internal static class SemanticDegradationFixture
{
    /// <summary>The component text every degradation scenario opens: one declared member.</summary>
    internal const string ComponentText =
        "@script {\n    public Reference<int> Count = Reactive.Reference(0);\n    \n}\n";

    /// <summary>Creates an isolated fixture root under the system temporary directory.</summary>
    /// <param name="scenario">A short scenario name embedded in the directory name.</param>
    internal static string CreateFixtureRoot(string scenario)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-semantic-degradation-tests",
            scenario + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Writes a Viu-owned project (an <c>Assimalign.Viu.Sdk</c> csproj plus the component file)
    /// with no restore state. The project file timestamp is backdated so a project-state file a
    /// test writes later is never mistaken for stale.
    /// </summary>
    /// <param name="projectDirectory">The project directory (created when absent).</param>
    /// <returns>The component file path.</returns>
    internal static string CreateProjectWithoutState(string projectDirectory)
    {
        Directory.CreateDirectory(projectDirectory);
        var projectFilePath = Path.Combine(projectDirectory, "App.csproj");
        File.WriteAllText(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
        File.SetLastWriteTimeUtc(projectFilePath, DateTime.UtcNow.AddMinutes(-10));
        var componentPath = Path.Combine(projectDirectory, "Counter.viu");
        File.WriteAllText(componentPath, ComponentText);
        return componentPath;
    }

    /// <summary>
    /// Writes the smallest resolvable project state: an empty RID-less target with no packages and
    /// no framework references resolves to zero reference assemblies and activates semantics.
    /// </summary>
    /// <param name="projectDirectory">The project directory.</param>
    internal static void WriteResolvableProjectState(string projectDirectory)
        => WriteProjectState(
            projectDirectory,
            targets: [],
            libraries: [],
            packageFolders: []);

    /// <summary>
    /// Writes a real-shaped project state whose one package compile asset is absent on disk: the
    /// declared package folder inside the fixture carries neither the assembly nor NuGet's
    /// <c>.nupkg.metadata</c> restore-success marker, so resolution degrades with a detail naming
    /// the package and the restore remedy.
    /// </summary>
    /// <param name="projectDirectory">The project directory.</param>
    /// <returns>The library key of the unrestored package.</returns>
    internal static string WriteUnrestoredPackageProjectState(string projectDirectory)
    {
        const string libraryKey = "Fixture.Package/1.0.0";
        var packagesFolder = Path.Combine(projectDirectory, "packages");
        Directory.CreateDirectory(packagesFolder);
        WriteProjectState(
            projectDirectory,
            targets: new Dictionary<string, object?>
            {
                [libraryKey] = new Dictionary<string, object?>
                {
                    ["type"] = "package",
                    ["compile"] = new Dictionary<string, object?>
                    {
                        ["lib/net10.0/Fixture.Package.dll"] = new Dictionary<string, object?>(),
                    },
                },
            },
            libraries: new Dictionary<string, object?>
            {
                [libraryKey] = new Dictionary<string, object?>
                {
                    ["type"] = "package",
                    ["path"] = "fixture.package/1.0.0",
                },
            },
            packageFolders: [packagesFolder]);
        return libraryKey;
    }

    /// <summary>Deletes the project state, re-degrading the project to missing artifacts.</summary>
    /// <param name="projectDirectory">The project directory.</param>
    internal static void DeleteProjectState(string projectDirectory)
        => Directory.Delete(Path.Combine(projectDirectory, "obj"), recursive: true);

    private static void WriteProjectState(
        string projectDirectory,
        Dictionary<string, object?> targets,
        Dictionary<string, object?> libraries,
        string[] packageFolders)
    {
        var packageFolderEntries = new Dictionary<string, object?>();
        foreach (var packageFolder in packageFolders)
        {
            packageFolderEntries[packageFolder] = new Dictionary<string, object?>();
        }

        var assets = new Dictionary<string, object?>
        {
            ["version"] = 4,
            ["targets"] = new Dictionary<string, object?>
            {
                ["net10.0"] = targets,
            },
            ["libraries"] = libraries,
            ["packageFolders"] = packageFolderEntries,
            ["project"] = new Dictionary<string, object?>
            {
                ["version"] = "1.0.0",
                ["frameworks"] = new Dictionary<string, object?>
                {
                    ["net10.0"] = new Dictionary<string, object?>(),
                },
            },
        };
        var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(assetsFilePath)!);
        File.WriteAllText(assetsFilePath, JsonSerializer.Serialize(assets));
    }
}
