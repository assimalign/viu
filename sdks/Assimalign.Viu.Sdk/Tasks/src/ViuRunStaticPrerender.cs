using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Assimalign.Viu.Sdk.Tasks;

/// <summary>
/// Runs an explicitly built application's static-prerender host through the .NET executable.
/// </summary>
/// <remarks>
/// Arguments are passed directly to the process, never through a command shell. Standard output
/// reports emitted documents and a nonzero exit code fails the build. The inherited tool-task
/// lifetime supports build cancellation. Specified by <c>[SSG-6]</c> and <c>[PKG-4]</c>.
/// </remarks>
public sealed class ViuRunStaticPrerender : ToolTask
{
    /// <summary>Gets or sets the built, explicitly designated server application's assembly path.</summary>
    [Required]
    public string ApplicationAssemblyPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory receiving one UTF-8 document per route.</summary>
    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the published host page whose asset references must be retained.</summary>
    [Required]
    public string HostPagePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the server project's directory, used as the child working directory.</summary>
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets base-stripped route locations, each passed as a separate argument.</summary>
    public ITaskItem[] Routes { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>Gets or sets an optional UTF-8 file containing one route location per line.</summary>
    public string RoutesFilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the memory-history base; this does not rewrite host-page assets.</summary>
    public string BasePath { get; set; } = "/";

    /// <summary>Gets or sets the .NET host path; the current host or PATH is used when omitted.</summary>
    public string DotNetHostPath { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override string ToolName => Path.GetFileName(GenerateFullPathToTool());

    /// <inheritdoc />
    protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

    /// <inheritdoc />
    protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

    /// <inheritdoc />
    protected override string GetWorkingDirectory() => ProjectDirectory;

    /// <inheritdoc />
    protected override string GenerateFullPathToTool()
    {
        string? configuredHost = string.IsNullOrWhiteSpace(DotNetHostPath)
            ? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            : DotNetHostPath;
        return string.IsNullOrWhiteSpace(configuredHost) ? "dotnet" : configuredHost!;
    }

    /// <inheritdoc />
    protected override string GenerateCommandLineCommands()
    {
        var arguments = new List<string>
        {
            "exec", ApplicationAssemblyPath, "prerender",
            "--output", OutputDirectory,
            "--host-page", HostPagePath,
            "--base", BasePath,
        };
        foreach (ITaskItem route in Routes)
        {
            arguments.Add("--route");
            arguments.Add(route.ItemSpec);
        }

        if (!string.IsNullOrWhiteSpace(RoutesFilePath))
        {
            arguments.Add("--routes");
            arguments.Add(RoutesFilePath);
        }

        // The task targets netstandard2.0, where ProcessStartInfo.ArgumentList is unavailable.
        // Quote every argument using the managed process command-line grammar; no shell is used.
        var commandLine = new StringBuilder();
        foreach (string argument in arguments)
        {
            if (commandLine.Length != 0)
            {
                commandLine.Append(' ');
            }

            commandLine.Append('"');
            int backslashCount = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                commandLine.Append('\\', character == '"' ? backslashCount * 2 + 1 : backslashCount);
                commandLine.Append(character);
                backslashCount = 0;
            }

            commandLine.Append('\\', backslashCount * 2);
            commandLine.Append('"');
        }

        return commandLine.ToString();
    }
}
