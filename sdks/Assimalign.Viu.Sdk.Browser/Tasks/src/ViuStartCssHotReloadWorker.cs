using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

/// <summary>
/// Starts the development-only Viu generated-asset regeneration worker for a
/// <c>dotnet watch</c> session.
/// </summary>
/// <remarks>
/// The task waits until the worker records its process identity before returning. This lets the worker
/// capture the owning <c>dotnet watch</c> process while the short-lived watch-list MSBuild process is
/// still alive. A project-scoped state file and the worker's named mutex prevent duplicate workers.
/// The task serializes the public <c>ViuGeneratedAsset</c> contract to a private, project-scoped
/// worker configuration so provider metadata never depends on command-line quoting.
/// Specified by <c>[V01.01.12.30.04]</c> (#355).
/// </remarks>
public sealed class ViuStartCssHotReloadWorker : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// Gets or sets the absolute path to the worker assembly.
    /// </summary>
    [Required]
    public string WorkerAssemblyPath { get; set; } = string.Empty;

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
    /// Gets or sets the .NET host used to run the worker and nested MSBuild regeneration.
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

        if (TryReadLiveWorker(StateFilePath, out var existingProcessIdentifier))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Viu Generated Asset Hot Reload worker {0} is already active for {1}.",
                existingProcessIdentifier,
                ProjectPath);
            return true;
        }

        try
        {
            if (File.Exists(StateFilePath))
            {
                File.Delete(StateFilePath);
            }

            if (!File.Exists(WorkerAssemblyPath))
            {
                Log.LogError(
                    "Viu Generated Asset Hot Reload worker assembly was not found at '{0}'.",
                    WorkerAssemblyPath);
                return false;
            }

            if (!File.Exists(ProjectPath))
            {
                Log.LogError(
                    "Viu Generated Asset Hot Reload project was not found at '{0}'.",
                    ProjectPath);
                return false;
            }

            var stateDirectory = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrEmpty(stateDirectory))
            {
                Directory.CreateDirectory(stateDirectory);
            }

            var dotNetHostPath = ResolveDotNetHostPath();
            var configurationFilePath = StateFilePath + ".configuration";
            GeneratedAssetWorkerConfigurationWriter.Write(
                configurationFilePath,
                ProjectPath,
                ProjectDirectory,
                dotNetHostPath,
                Configuration,
                TargetFramework,
                RuntimeIdentifier,
                StateFilePath,
                EventLogPath,
                GetCurrentProcessIdentifier(),
                DebounceMilliseconds,
                GeneratedAssets,
                ExcludedDirectories);

            var startInfo = CreateStartInfo(
                configurationFilePath,
                dotNetHostPath);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Log.LogError("Viu Generated Asset Hot Reload worker could not be started.");
                return false;
            }

            var readyDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < readyDeadline)
            {
                if (TryReadLiveWorker(StateFilePath, out var workerProcessIdentifier))
                {
                    Log.LogMessage(
                        MessageImportance.Normal,
                        "Viu Generated Asset Hot Reload worker {0} is watching {1}.",
                        workerProcessIdentifier,
                        ProjectDirectory);
                    return true;
                }

                if (process.HasExited)
                {
                    Log.LogError(
                        "Viu Generated Asset Hot Reload worker exited before initialization with code {0}.",
                        process.ExitCode);
                    return false;
                }

                Thread.Sleep(25);
            }

            Log.LogError(
                "Viu Generated Asset Hot Reload worker did not initialize within five seconds.");
            return false;
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

    private ProcessStartInfo CreateStartInfo(
        string configurationFilePath,
        string dotNetHostPath)
    {
        var arguments = new List<string>
        {
            WorkerAssemblyPath,
            "--configuration-file",
            configurationFilePath,
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = dotNetHostPath,
            Arguments = JoinArguments(arguments),
            WorkingDirectory = ProjectDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.EnvironmentVariables["VIU_GENERATED_ASSET_HOT_RELOAD"] = "1";
        return startInfo;
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

    private static int GetCurrentProcessIdentifier()
    {
        using var process = Process.GetCurrentProcess();
        return process.Id;
    }

    private static bool TryReadLiveWorker(
        string stateFilePath,
        out int processIdentifier)
    {
        processIdentifier = 0;
        try
        {
            if (!File.Exists(stateFilePath))
            {
                return false;
            }

            var processStartTicks = 0L;
            foreach (var line in File.ReadAllLines(stateFilePath))
            {
                if (line.StartsWith("worker=", StringComparison.Ordinal))
                {
                    int.TryParse(
                        line.Substring("worker=".Length),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out processIdentifier);
                }
                else if (line.StartsWith("worker-start=", StringComparison.Ordinal))
                {
                    long.TryParse(
                        line.Substring("worker-start=".Length),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out processStartTicks);
                }
            }

            if (processIdentifier <= 0 || processStartTicks <= 0)
            {
                return false;
            }

            using var process = Process.GetProcessById(processIdentifier);
            return !process.HasExited &&
                process.StartTime.ToUniversalTime().Ticks == processStartTicks;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidOperationException or
                IOException or
                UnauthorizedAccessException)
        {
            processIdentifier = 0;
            return false;
        }
    }

    private static string JoinArguments(IEnumerable<string> arguments)
    {
        var builder = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            AppendQuotedArgument(builder, argument);
        }

        return builder.ToString();
    }

    private static void AppendQuotedArgument(StringBuilder builder, string argument)
    {
        builder.Append('"');
        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            builder.Append(character);
            backslashCount = 0;
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
    }
}
