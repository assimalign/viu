using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

/// <summary>
/// Writes the deterministic, project-scoped configuration consumed by the Viu generated-asset
/// worker during a development session.
/// </summary>
/// <remarks>
/// The configuration contains the public <c>ViuGeneratedAsset</c> descriptions and stable build
/// settings only. Process ownership is supplied when a development host launches the worker, so an
/// unchanged ordinary Debug build retains the configuration file's timestamp. The task never starts
/// a worker. Specified by <c>[V01.01.12.30.05]</c> (#357).
/// </remarks>
public sealed class ViuWriteGeneratedAssetWorkerConfiguration : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// Gets or sets the configuration file to write.
    /// </summary>
    [Required]
    public string ConfigurationFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the consuming project.
    /// </summary>
    [Required]
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the consuming project's absolute directory.
    /// </summary>
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project-scoped worker state file.
    /// </summary>
    [Required]
    public string StateFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional diagnostic event-log path used by deterministic integration tests.
    /// </summary>
    public string EventLogPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the .NET host used for nested MSBuild regeneration.
    /// </summary>
    public string DotNetHostPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active build configuration.
    /// </summary>
    public string Configuration { get; set; } = "Debug";

    /// <summary>
    /// Gets or sets the active target framework, when one was selected.
    /// </summary>
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active runtime identifier, when one was selected.
    /// </summary>
    public string RuntimeIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated assets and their documented watch and regeneration metadata.
    /// </summary>
    /// <remarks>
    /// Each item identity is the generated output path. The task consumes <c>WatchFiles</c>,
    /// <c>WatchRoots</c>, <c>WatchExtensions</c>, <c>RegenerationTarget</c>, optional
    /// <c>DependencyManifestPath</c>, <c>StaticWebAssetPath</c>, and <c>RemovalBehavior</c> metadata.
    /// Specified by <c>[V01.01.12.30.04]</c> (#355).
    /// </remarks>
    public ITaskItem[] GeneratedAssets { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Gets or sets project directories excluded from recursive observation.
    /// </summary>
    public ITaskItem[] ExcludedDirectories { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Gets or sets the quiet-period duration used to coalesce one editor save into one regeneration.
    /// </summary>
    public int DebounceMilliseconds { get; set; } = 100;

    /// <inheritdoc />
    public override bool Execute()
    {
        if (GeneratedAssets.Length == 0)
        {
            return true;
        }

        try
        {
            GeneratedAssetWorkerConfigurationWriter.Write(
                ConfigurationFilePath,
                ProjectPath,
                ProjectDirectory,
                ResolveDotNetHostPath(),
                Configuration,
                TargetFramework,
                RuntimeIdentifier,
                StateFilePath,
                EventLogPath,
                DebounceMilliseconds,
                GeneratedAssets,
                ExcludedDirectories);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidOperationException or
                IOException or
                UnauthorizedAccessException)
        {
            Log.LogErrorFromException(exception, showStackTrace: false);
            return false;
        }
    }

    private string ResolveDotNetHostPath()
    {
        var dotNetHostPath = DotNetHostPath;
        if (string.IsNullOrWhiteSpace(dotNetHostPath))
        {
            dotNetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        }

        return string.IsNullOrWhiteSpace(dotNetHostPath)
            ? "dotnet"
            : dotNetHostPath;
    }
}
