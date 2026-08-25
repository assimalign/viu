using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class CssHotReloadOptions
{
    private const string ConfigurationHeader =
        "viu-generated-asset-worker-configuration-v1";

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

    public int? LauncherProcessIdentifier { get; private set; }

    public int? OwnerProcessIdentifier { get; private set; }

    public int DebounceMilliseconds { get; private set; } = 100;

    public IReadOnlyList<GeneratedAssetDescriptor> GeneratedAssets { get; private set; } =
        Array.Empty<GeneratedAssetDescriptor>();

    public IReadOnlyList<string> ExcludedDirectories { get; private set; } =
        Array.Empty<string>();

    public static bool TryParse(
        string[] arguments,
        TextWriter errorWriter,
        out CssHotReloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(errorWriter);

        options = new CssHotReloadOptions();
        if (arguments.Length != 2 ||
            !string.Equals(
                arguments[0],
                "--configuration-file",
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(arguments[1]))
        {
            errorWriter.WriteLine(
                "Viu Generated Asset Hot Reload requires --configuration-file <path>.");
            return false;
        }

        try
        {
            return TryReadConfiguration(
                Path.GetFullPath(arguments[1]),
                errorWriter,
                options);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException or
                FormatException)
        {
            errorWriter.WriteLine(
                "Viu Generated Asset Hot Reload could not read its configuration: " +
                exception.Message);
            return false;
        }
    }

    private static bool TryReadConfiguration(
        string path,
        TextWriter errorWriter,
        CssHotReloadOptions options)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0 ||
            !string.Equals(lines[0], ConfigurationHeader, StringComparison.Ordinal))
        {
            errorWriter.WriteLine(
                "Viu Generated Asset Hot Reload configuration has an unsupported header.");
            return false;
        }

        var excludedDirectories = new List<string>();
        var generatedAssets = new List<GeneratedAssetDescriptor>();
        AssetBuilder? asset = null;
        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.Equals(line, "asset-begin", StringComparison.Ordinal))
            {
                if (asset is not null)
                {
                    return ConfigurationError(
                        errorWriter,
                        "contains a nested asset record");
                }

                asset = new AssetBuilder();
                continue;
            }

            if (string.Equals(line, "asset-end", StringComparison.Ordinal))
            {
                if (asset is null ||
                    !asset.TryBuild(errorWriter, out var descriptor))
                {
                    return false;
                }

                generatedAssets.Add(descriptor);
                asset = null;
                continue;
            }

            if (!TrySplitLine(line, out var name, out var encodedValue))
            {
                return ConfigurationError(
                    errorWriter,
                    "contains an invalid record");
            }

            var value = Decode(encodedValue);
            if (asset is not null)
            {
                if (!asset.TrySet(name, value, errorWriter))
                {
                    return false;
                }

                continue;
            }

            switch (name)
            {
                case "project-path":
                    options.ProjectPath = Path.GetFullPath(value);
                    break;
                case "project-directory":
                    options.ProjectDirectory = Path.GetFullPath(value);
                    break;
                case "dotnet-host":
                    options.DotNetHostPath = value;
                    break;
                case "configuration":
                    options.Configuration = value;
                    break;
                case "target-framework":
                    options.TargetFramework = value;
                    break;
                case "runtime-identifier":
                    options.RuntimeIdentifier = value;
                    break;
                case "state-file":
                    options.StateFilePath = Path.GetFullPath(value);
                    break;
                case "event-log":
                    options.EventLogPath = string.IsNullOrEmpty(value)
                        ? string.Empty
                        : Path.GetFullPath(value);
                    break;
                case "launcher-process-identifier":
                    if (!TryParsePositiveInteger(value, out var launcherProcessIdentifier))
                    {
                        return ConfigurationError(
                            errorWriter,
                            "contains an invalid launcher process identifier");
                    }

                    options.LauncherProcessIdentifier = launcherProcessIdentifier;
                    break;
                case "owner-process-identifier":
                    if (!TryParsePositiveInteger(value, out var ownerProcessIdentifier))
                    {
                        return ConfigurationError(
                            errorWriter,
                            "contains an invalid owner process identifier");
                    }

                    options.OwnerProcessIdentifier = ownerProcessIdentifier;
                    break;
                case "debounce-milliseconds":
                    if (!TryParsePositiveInteger(value, out var debounceMilliseconds))
                    {
                        return ConfigurationError(
                            errorWriter,
                            "contains an invalid debounce duration");
                    }

                    options.DebounceMilliseconds = debounceMilliseconds;
                    break;
                case "excluded-directory":
                    excludedDirectories.Add(Path.GetFullPath(value));
                    break;
                default:
                    return ConfigurationError(
                        errorWriter,
                        "contains unknown record '" + name + "'");
            }
        }

        if (asset is not null)
        {
            return ConfigurationError(errorWriter, "contains an unterminated asset record");
        }

        if (string.IsNullOrEmpty(options.ProjectPath) ||
            string.IsNullOrEmpty(options.ProjectDirectory) ||
            string.IsNullOrEmpty(options.StateFilePath) ||
            generatedAssets.Count == 0)
        {
            return ConfigurationError(
                errorWriter,
                "must declare the project, state file, and at least one asset");
        }

        if (options.OwnerProcessIdentifier is null &&
            options.LauncherProcessIdentifier is null)
        {
            return ConfigurationError(
                errorWriter,
                "must declare an owner or launcher process identifier");
        }

        options.GeneratedAssets = generatedAssets;
        options.ExcludedDirectories = excludedDirectories;
        return true;
    }

    private static bool TrySplitLine(
        string line,
        out string name,
        out string encodedValue)
    {
        var separator = line.IndexOf(':');
        if (separator <= 0)
        {
            name = string.Empty;
            encodedValue = string.Empty;
            return false;
        }

        name = line.Substring(0, separator);
        encodedValue = line.Substring(separator + 1);
        return true;
    }

    private static string Decode(string encodedValue) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue));

    private static bool TryParsePositiveInteger(string value, out int result) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out result) &&
        result > 0;

    private static bool ConfigurationError(TextWriter errorWriter, string message)
    {
        errorWriter.WriteLine(
            "Viu Generated Asset Hot Reload configuration " + message + ".");
        return false;
    }

    private sealed class AssetBuilder
    {
        private readonly List<string> watchFiles = new List<string>();
        private readonly List<string> watchRoots = new List<string>();
        private readonly List<string> watchExtensions = new List<string>();
        private string identity = string.Empty;
        private string regenerationTarget = string.Empty;
        private string dependencyManifestPath = string.Empty;
        private string staticWebAssetPath = string.Empty;
        private string removalBehavior = string.Empty;

        public bool TrySet(string name, string value, TextWriter errorWriter)
        {
            switch (name)
            {
                case "identity":
                    identity = Path.GetFullPath(value);
                    break;
                case "watch-file":
                    watchFiles.Add(Path.GetFullPath(value));
                    break;
                case "watch-root":
                    watchRoots.Add(Path.GetFullPath(value));
                    break;
                case "watch-extension":
                    watchExtensions.Add(value);
                    break;
                case "regeneration-target":
                    regenerationTarget = value;
                    break;
                case "dependency-manifest-path":
                    dependencyManifestPath = Path.GetFullPath(value);
                    break;
                case "static-web-asset-path":
                    staticWebAssetPath = value;
                    break;
                case "removal-behavior":
                    removalBehavior = value;
                    break;
                default:
                    return ConfigurationError(
                        errorWriter,
                        "contains unknown asset record '" + name + "'");
            }

            return true;
        }

        public bool TryBuild(
            TextWriter errorWriter,
            out GeneratedAssetDescriptor descriptor)
        {
            descriptor = null!;
            if (string.IsNullOrEmpty(identity) ||
                string.IsNullOrWhiteSpace(regenerationTarget) ||
                string.IsNullOrWhiteSpace(staticWebAssetPath) ||
                (!string.Equals(removalBehavior, "Delete", StringComparison.Ordinal) &&
                    !string.Equals(
                        removalBehavior,
                        "PreserveEmpty",
                        StringComparison.Ordinal)))
            {
                return ConfigurationError(
                    errorWriter,
                    "contains an incomplete asset record");
            }

            if (watchFiles.Count == 0 &&
                watchRoots.Count == 0 &&
                string.IsNullOrEmpty(dependencyManifestPath))
            {
                return ConfigurationError(
                    errorWriter,
                    "contains an asset without watched inputs");
            }

            if (watchRoots.Count > 0 && watchExtensions.Count == 0)
            {
                return ConfigurationError(
                    errorWriter,
                    "contains a watch root without extensions");
            }

            descriptor = new GeneratedAssetDescriptor(
                identity,
                watchFiles,
                watchRoots,
                watchExtensions,
                regenerationTarget,
                dependencyManifestPath,
                staticWebAssetPath,
                removalBehavior);
            return true;
        }
    }
}
