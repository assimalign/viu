using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class CssHotReloadOptions
{
    private CssHotReloadOptions()
    {
    }

    public string ProjectPath { get; private set; } = string.Empty;

    public string ProjectDirectory { get; private set; } = string.Empty;

    public string DotNetHostPath { get; private set; } = "dotnet";

    public string Configuration { get; private set; } = "Debug";

    public string TargetFramework { get; private set; } = string.Empty;

    public string RuntimeIdentifier { get; private set; } = string.Empty;

    public string StateFilePath { get; private set; } = string.Empty;

    public string EventLogPath { get; private set; } = string.Empty;

    public string DependencyManifestPath { get; private set; } = string.Empty;

    public int? LauncherProcessIdentifier { get; private set; }

    public int? OwnerProcessIdentifier { get; private set; }

    public int DebounceMilliseconds { get; private set; } = 100;

    public bool WatchComponents { get; private set; }

    public bool WatchUtilityMarkup { get; private set; }

    public IReadOnlyList<string> ExplicitWatchFiles { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<string> ExcludedDirectories { get; private set; } = Array.Empty<string>();

    public static bool TryParse(
        string[] arguments,
        TextWriter errorWriter,
        out CssHotReloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(errorWriter);

        options = new CssHotReloadOptions();
        var explicitWatchFiles = new List<string>();
        var excludedDirectories = new List<string>();
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--project":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var projectPath))
                    {
                        return false;
                    }

                    options.ProjectPath = Path.GetFullPath(projectPath);
                    break;

                case "--project-directory":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var projectDirectory))
                    {
                        return false;
                    }

                    options.ProjectDirectory = Path.GetFullPath(projectDirectory);
                    break;

                case "--dotnet-host":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var dotNetHostPath))
                    {
                        return false;
                    }

                    options.DotNetHostPath = dotNetHostPath;
                    break;

                case "--configuration":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var configuration))
                    {
                        return false;
                    }

                    options.Configuration = configuration;
                    break;

                case "--target-framework":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var targetFramework))
                    {
                        return false;
                    }

                    options.TargetFramework = targetFramework;
                    break;

                case "--runtime-identifier":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var runtimeIdentifier))
                    {
                        return false;
                    }

                    options.RuntimeIdentifier = runtimeIdentifier;
                    break;

                case "--state-file":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var stateFilePath))
                    {
                        return false;
                    }

                    options.StateFilePath = Path.GetFullPath(stateFilePath);
                    break;

                case "--event-log":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var eventLogPath))
                    {
                        return false;
                    }

                    options.EventLogPath = Path.GetFullPath(eventLogPath);
                    break;

                case "--dependency-manifest":
                    if (!TryReadValue(
                            arguments,
                            ref index,
                            argument,
                            errorWriter,
                            out var dependencyManifestPath))
                    {
                        return false;
                    }

                    options.DependencyManifestPath =
                        Path.GetFullPath(dependencyManifestPath);
                    break;

                case "--launcher-process-id":
                    if (!TryReadPositiveInteger(
                            arguments,
                            ref index,
                            argument,
                            errorWriter,
                            out var launcherProcessIdentifier))
                    {
                        return false;
                    }

                    options.LauncherProcessIdentifier = launcherProcessIdentifier;
                    break;

                case "--owner-process-id":
                    if (!TryReadPositiveInteger(
                            arguments,
                            ref index,
                            argument,
                            errorWriter,
                            out var ownerProcessIdentifier))
                    {
                        return false;
                    }

                    options.OwnerProcessIdentifier = ownerProcessIdentifier;
                    break;

                case "--debounce-milliseconds":
                    if (!TryReadPositiveInteger(
                            arguments,
                            ref index,
                            argument,
                            errorWriter,
                            out var debounceMilliseconds))
                    {
                        return false;
                    }

                    options.DebounceMilliseconds = debounceMilliseconds;
                    break;

                case "--watch-components":
                    options.WatchComponents = true;
                    break;

                case "--watch-utility-markup":
                    options.WatchUtilityMarkup = true;
                    break;

                case "--watch-file":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var watchFile))
                    {
                        return false;
                    }

                    explicitWatchFiles.Add(Path.GetFullPath(watchFile));
                    break;

                case "--exclude-directory":
                    if (!TryReadValue(arguments, ref index, argument, errorWriter, out var excludedDirectory))
                    {
                        return false;
                    }

                    excludedDirectories.Add(Path.GetFullPath(excludedDirectory));
                    break;

                default:
                    errorWriter.WriteLine("Unknown CSS Hot Reload worker argument: " + argument);
                    return false;
            }
        }

        if (string.IsNullOrEmpty(options.ProjectPath) ||
            string.IsNullOrEmpty(options.ProjectDirectory) ||
            string.IsNullOrEmpty(options.StateFilePath))
        {
            errorWriter.WriteLine(
                "--project, --project-directory, and --state-file are required.");
            return false;
        }

        if (options.OwnerProcessIdentifier is null &&
            options.LauncherProcessIdentifier is null)
        {
            errorWriter.WriteLine(
                "Either --owner-process-id or --launcher-process-id is required.");
            return false;
        }

        options.ExplicitWatchFiles = explicitWatchFiles;
        options.ExcludedDirectories = excludedDirectories;
        return true;
    }

    private static bool TryReadPositiveInteger(
        string[] arguments,
        ref int index,
        string option,
        TextWriter errorWriter,
        out int value)
    {
        value = 0;
        if (!TryReadValue(arguments, ref index, option, errorWriter, out var text) ||
            !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) ||
            value <= 0)
        {
            errorWriter.WriteLine(option + " requires a positive integer.");
            return false;
        }

        return true;
    }

    private static bool TryReadValue(
        string[] arguments,
        ref int index,
        string option,
        TextWriter errorWriter,
        out string value)
    {
        if (index + 1 >= arguments.Length)
        {
            errorWriter.WriteLine(option + " requires a value.");
            value = string.Empty;
            return false;
        }

        value = arguments[++index];
        return !string.IsNullOrWhiteSpace(value);
    }
}
