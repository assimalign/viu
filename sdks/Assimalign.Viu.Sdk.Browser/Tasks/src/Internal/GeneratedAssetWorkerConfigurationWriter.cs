using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

internal static class GeneratedAssetWorkerConfigurationWriter
{
    internal const string Header = "viu-generated-asset-worker-configuration-v1";
    private static readonly StringComparer PathComparer =
        Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public static void Write(
        string configurationFilePath,
        string projectPath,
        string projectDirectory,
        string dotNetHostPath,
        string configuration,
        string targetFramework,
        string runtimeIdentifier,
        string stateFilePath,
        string eventLogPath,
        int debounceMilliseconds,
        IEnumerable<ITaskItem> generatedAssets,
        IEnumerable<ITaskItem> excludedDirectories)
    {
        var fullProjectDirectory = Path.GetFullPath(projectDirectory);
        var lines = new List<string>
        {
            Header,
            Encode("project-path", ResolvePath(projectPath, fullProjectDirectory)),
            Encode("project-directory", fullProjectDirectory),
            Encode("dotnet-host", dotNetHostPath),
            Encode("configuration", configuration),
            Encode("target-framework", targetFramework),
            Encode("runtime-identifier", runtimeIdentifier),
            Encode("state-file", ResolvePath(stateFilePath, fullProjectDirectory)),
            Encode("event-log", ResolveOptionalTaskPath(eventLogPath, fullProjectDirectory)),
            Encode(
                "debounce-milliseconds",
                Math.Max(1, debounceMilliseconds).ToString(CultureInfo.InvariantCulture)),
        };

        foreach (var excludedDirectory in excludedDirectories)
        {
            if (excludedDirectory is null ||
                string.IsNullOrWhiteSpace(excludedDirectory.ItemSpec))
            {
                continue;
            }

            lines.Add(Encode(
                "excluded-directory",
                ResolvePath(excludedDirectory.ItemSpec, fullProjectDirectory)));
        }

        foreach (var generatedAsset in generatedAssets)
        {
            if (generatedAsset is null ||
                string.IsNullOrWhiteSpace(generatedAsset.ItemSpec))
            {
                throw new InvalidOperationException(
                    "ViuGeneratedAsset Identity must be a generated asset path.");
            }

            var watchFiles = SplitMetadata(generatedAsset, "WatchFiles")
                .Select(value => NormalizeContractPath(
                    value,
                    generatedAsset,
                    "WatchFiles"))
                .Distinct(PathComparer)
                .ToArray();
            var watchRoots = SplitMetadata(generatedAsset, "WatchRoots")
                .Select(value => NormalizeContractPath(
                    value,
                    generatedAsset,
                    "WatchRoots"))
                .Distinct(PathComparer)
                .ToArray();
            var watchExtensions = SplitMetadata(generatedAsset, "WatchExtensions")
                .Select(NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var regenerationTarget = RequiredMetadata(
                generatedAsset,
                "RegenerationTarget");
            var dependencyManifestPath = ResolveOptionalPath(
                generatedAsset.GetMetadata("DependencyManifestPath"),
                generatedAsset,
                "DependencyManifestPath");
            var staticWebAssetPath = RequiredMetadata(
                generatedAsset,
                "StaticWebAssetPath");
            var removalBehavior = RequiredMetadata(
                generatedAsset,
                "RemovalBehavior");

            if (regenerationTarget.IndexOf(';') >= 0)
            {
                throw new InvalidOperationException(
                    "Each ViuGeneratedAsset RegenerationTarget must name one MSBuild target.");
            }

            ValidateStaticWebAssetPath(staticWebAssetPath);

            if (watchFiles.Length == 0 &&
                watchRoots.Length == 0 &&
                string.IsNullOrEmpty(dependencyManifestPath))
            {
                throw new InvalidOperationException(
                    "ViuGeneratedAsset '" + generatedAsset.ItemSpec +
                    "' must declare WatchFiles, WatchRoots, or DependencyManifestPath.");
            }

            if (watchRoots.Length > 0 && watchExtensions.Length == 0)
            {
                throw new InvalidOperationException(
                    "ViuGeneratedAsset '" + generatedAsset.ItemSpec +
                    "' must declare WatchExtensions when WatchRoots are present.");
            }

            if (!string.Equals(removalBehavior, "Delete", StringComparison.Ordinal) &&
                !string.Equals(removalBehavior, "PreserveEmpty", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "ViuGeneratedAsset RemovalBehavior must be Delete or PreserveEmpty.");
            }

            lines.Add("asset-begin");
            lines.Add(Encode(
                "identity",
                NormalizeContractPath(
                    generatedAsset.ItemSpec,
                    generatedAsset,
                    "Identity")));
            AddEncodedValues(lines, "watch-file", watchFiles);
            AddEncodedValues(lines, "watch-root", watchRoots);
            AddEncodedValues(lines, "watch-extension", watchExtensions);
            lines.Add(Encode("regeneration-target", regenerationTarget));
            if (!string.IsNullOrEmpty(dependencyManifestPath))
            {
                lines.Add(Encode(
                    "dependency-manifest-path",
                    dependencyManifestPath));
            }

            lines.Add(Encode("static-web-asset-path", staticWebAssetPath));
            lines.Add(Encode("removal-behavior", removalBehavior));
            lines.Add("asset-end");
        }

        var directory = Path.GetDirectoryName(configurationFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(content);
        if (File.Exists(configurationFilePath) &&
            File.ReadAllBytes(configurationFilePath).SequenceEqual(bytes))
        {
            return;
        }

        File.WriteAllBytes(configurationFilePath, bytes);
    }

    private static void AddEncodedValues(
        ICollection<string> lines,
        string name,
        IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            lines.Add(Encode(name, value));
        }
    }

    private static string RequiredMetadata(ITaskItem item, string name)
    {
        var value = item.GetMetadata(name).Trim();
        if (value.Length == 0)
        {
            throw new InvalidOperationException(
                "ViuGeneratedAsset '" + item.ItemSpec +
                "' must declare " + name + ".");
        }

        return value;
    }

    private static IEnumerable<string> SplitMetadata(ITaskItem item, string name) =>
        item.GetMetadata(name)
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0);

    private static string NormalizeExtension(string extension)
    {
        var value = extension.Trim();
        if (value.Length < 2 ||
            value[0] != '.' ||
            value.IndexOfAny(new[]
            {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar,
                '*',
                '?',
            }) >= 0)
        {
            throw new InvalidOperationException(
                "ViuGeneratedAsset WatchExtensions entries must be file extensions.");
        }

        return value;
    }

    private static string ResolveOptionalPath(
        string path,
        ITaskItem item,
        string metadataName) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : NormalizeContractPath(path, item, metadataName);

    private static string ResolveOptionalTaskPath(string path, string projectDirectory) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : ResolvePath(path, projectDirectory);

    private static string NormalizeContractPath(
        string path,
        ITaskItem item,
        string metadataName)
    {
        if (!Path.IsPathRooted(path))
        {
            throw new InvalidOperationException(
                "ViuGeneratedAsset '" + item.ItemSpec + "' " + metadataName +
                " paths must be absolute.");
        }

        return Path.GetFullPath(path);
    }

    private static void ValidateStaticWebAssetPath(string path)
    {
        if (!path.StartsWith("wwwroot/", StringComparison.Ordinal) ||
            path.Length == "wwwroot/".Length ||
            path.IndexOf('\\') >= 0 ||
            path.Split('/').Any(segment => segment is "." or ".." or ""))
        {
            throw new InvalidOperationException(
                "ViuGeneratedAsset StaticWebAssetPath must be a stable route beginning with wwwroot/.");
        }
    }

    private static string ResolvePath(string path, string projectDirectory) =>
        Path.GetFullPath(
            Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectDirectory, path));

    private static string Encode(string name, string value) =>
        name + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
