using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Resolves a Viu project's compile inputs from its restore artifacts ([V01.01.12.23], #259): a
/// pure <see cref="System.Text.Json"/> parse of <c>obj\project.assets.json</c> — no MSBuild, no
/// BuildHost, no project evaluation. Package compile assets resolve against the declared
/// <c>packageFolders</c> order using NuGet's <c>.nupkg.metadata</c> restore-success marker,
/// framework references resolve from the installed dotnet root
/// (<c>Microsoft.NETCore.App</c>) and from pinned <c>downloadDependencies</c> ref packages
/// (everything else), and project references probe the referenced project's built output. Every
/// failure degrades to a <see cref="ViuProjectContextStatus"/> naming what failed and the remedy —
/// resolution never throws for a malformed or incomplete restore.
/// </summary>
internal static class ViuProjectAssetsReader
{
    private const string NetCoreApplicationFrameworkName = "Microsoft.NETCore.App";
    private const string NetCoreApplicationTargetingPackName = "Microsoft.NETCore.App.Ref";

    internal static ViuProjectAssets Read(string projectFilePath)
    {
        var projectDirectory = Path.GetDirectoryName(projectFilePath) ?? string.Empty;
        var objDirectory = Path.Combine(projectDirectory, "obj");
        var assetsFilePath = Path.Combine(objDirectory, "project.assets.json");
        if (!File.Exists(assetsFilePath))
        {
            return new ViuProjectAssets
            {
                Status = ViuProjectContextStatus.AssetsMissing(projectFilePath),
                ProjectFilePath = projectFilePath,
            };
        }

        byte[] assetsBytes;
        try
        {
            assetsBytes = File.ReadAllBytes(assetsFilePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return CreateUnresolvable(
                projectFilePath,
                $"obj\\project.assets.json could not be read ({exception.Message}); run dotnet restore");
        }

        try
        {
            using var document = JsonDocument.Parse(assetsBytes);
            return ReadCore(
                document.RootElement,
                projectFilePath,
                projectDirectory,
                objDirectory,
                assetsFilePath,
                assetsBytes);
        }
        catch (JsonException exception)
        {
            return CreateUnresolvable(
                projectFilePath,
                $"obj\\project.assets.json could not be parsed ({exception.Message}); run dotnet restore");
        }
    }

    /// <summary>
    /// Resolves the dotnet installation root, in the spec's probe order: (a) the installation the
    /// restore itself used, derived by stripping the trailing
    /// <c>sdk\&lt;version&gt;\PortableRuntimeIdentifierGraph.json</c> from the recorded
    /// <c>runtimeIdentifierGraphPath</c>; (b) <c>DOTNET_ROOT_&lt;architecture&gt;</c> matching the
    /// process architecture, then <c>DOTNET_ROOT</c>; (c) <c>%ProgramFiles%\dotnet</c>.
    /// </summary>
    internal static string? ResolveDotnetRoot(
        string? runtimeIdentifierGraphPath,
        Func<string, string?> getEnvironmentVariable,
        string? programFilesDirectory)
    {
        if (!string.IsNullOrEmpty(runtimeIdentifierGraphPath))
        {
            var versionDirectory = Path.GetDirectoryName(runtimeIdentifierGraphPath);
            var sdkDirectory = versionDirectory is null
                ? null
                : Path.GetDirectoryName(versionDirectory);
            if (sdkDirectory is not null &&
                string.Equals(
                    Path.GetFileName(sdkDirectory),
                    "sdk",
                    StringComparison.OrdinalIgnoreCase))
            {
                var pinnedRoot = Path.GetDirectoryName(sdkDirectory);
                if (!string.IsNullOrEmpty(pinnedRoot) && Directory.Exists(pinnedRoot))
                {
                    return pinnedRoot;
                }
            }
        }

        var architectureVariableName =
            "DOTNET_ROOT_" +
            RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
        foreach (var variableName in (ReadOnlySpan<string>)[architectureVariableName, "DOTNET_ROOT"])
        {
            var environmentRoot = getEnvironmentVariable(variableName);
            if (!string.IsNullOrEmpty(environmentRoot) && Directory.Exists(environmentRoot))
            {
                return environmentRoot;
            }
        }

        if (!string.IsNullOrEmpty(programFilesDirectory))
        {
            var fallbackRoot = Path.Combine(programFilesDirectory, "dotnet");
            if (Directory.Exists(fallbackRoot))
            {
                return fallbackRoot;
            }
        }

        return null;
    }

    private static ViuProjectAssets ReadCore(
        JsonElement root,
        string projectFilePath,
        string projectDirectory,
        string objDirectory,
        string assetsFilePath,
        byte[] assetsBytes)
    {
        // Target selection: the RID-less entry of targets whose key equals the restored TFM. The
        // RID-specific targets (net10.0/browser-wasm) are ignored — compile assets are identical
        // by construction and the RID-less target always exists.
        if (!TryGetObject(root, "targets", out var targets) ||
            !TryGetObject(root, "project", out var project) ||
            !TryGetObject(project, "frameworks", out var frameworks))
        {
            return CreateUnresolvable(
                projectFilePath,
                "obj\\project.assets.json has no targets for the restored framework; run dotnet restore");
        }

        string? targetFramework = null;
        JsonElement targetElement = default;
        JsonElement frameworkElement = default;
        foreach (var framework in frameworks.EnumerateObject())
        {
            if (targets.TryGetProperty(framework.Name, out var candidate) &&
                candidate.ValueKind == JsonValueKind.Object)
            {
                targetFramework = framework.Name;
                targetElement = candidate;
                frameworkElement = framework.Value;
                break;
            }
        }

        if (targetFramework is null)
        {
            return CreateUnresolvable(
                projectFilePath,
                "obj\\project.assets.json has no targets for the restored framework; run dotnet restore");
        }

        var packageFolders = ReadPackageFolders(root);
        TryGetObject(root, "libraries", out var libraries);

        var references = new List<string>();
        var droppedProjectReferences = new List<string>();
        var failureDetail = ResolveLibraryReferences(
            targetElement,
            libraries,
            packageFolders,
            projectDirectory,
            targetFramework,
            references,
            droppedProjectReferences);
        failureDetail ??= ResolveFrameworkReferences(
            frameworkElement,
            targetFramework,
            packageFolders,
            references);
        if (failureDetail is not null)
        {
            return CreateUnresolvable(projectFilePath, failureDetail);
        }

        var (rootNamespace, resolvedProjectDirectory, configuration) = ReadNames(
            projectFilePath,
            projectDirectory,
            objDirectory);
        var (sourceFilePaths, componentFilePaths) = EnumerateSourceCone(projectDirectory);
        var cacheStamp = ComputeCacheStamp(
            assetsBytes,
            rootNamespace,
            resolvedProjectDirectory,
            configuration,
            sourceFilePaths.Concat(componentFilePaths));

        return new ViuProjectAssets
        {
            Status = CreateResolvedStatus(
                projectFilePath,
                assetsFilePath,
                droppedProjectReferences),
            ProjectFilePath = projectFilePath,
            TargetFramework = targetFramework,
            ProjectDirectory = resolvedProjectDirectory,
            RootNamespace = rootNamespace,
            Configuration = configuration,
            ReferenceAssemblyPaths = references,
            SourceFilePaths = sourceFilePaths,
            ComponentFilePaths = componentFilePaths,
            CacheStamp = cacheStamp,
        };
    }

    private static ViuProjectContextStatus CreateResolvedStatus(
        string projectFilePath,
        string assetsFilePath,
        List<string> droppedProjectReferences)
    {
        var droppedDetail = droppedProjectReferences.Count == 0
            ? null
            : string.Join(
                "; ",
                droppedProjectReferences.Select(
                    name => $"the referenced project '{name}' has no built output; build {name} once"));

        // A project file newer than the assets is a cheap stat: semantics still serve from the
        // stale artifact, and the status suggests a re-restore.
        if (File.GetLastWriteTimeUtc(projectFilePath) > File.GetLastWriteTimeUtc(assetsFilePath))
        {
            var staleDetail =
                "the project file is newer than obj\\project.assets.json; run dotnet restore to refresh";
            return ViuProjectContextStatus.AssetsStale(
                projectFilePath,
                droppedDetail is null ? staleDetail : staleDetail + "; " + droppedDetail);
        }

        return ViuProjectContextStatus.SemanticsActive(projectFilePath, droppedDetail);
    }

    private static string? ResolveLibraryReferences(
        JsonElement targetElement,
        JsonElement libraries,
        IReadOnlyList<string> packageFolders,
        string projectDirectory,
        string targetFramework,
        List<string> references,
        List<string> droppedProjectReferences)
    {
        foreach (var library in targetElement.EnumerateObject())
        {
            var libraryType = library.Value.ValueKind == JsonValueKind.Object &&
                library.Value.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : null;
            if (libraryType == "package")
            {
                var failureDetail = ResolvePackageReference(
                    library.Name,
                    library.Value,
                    libraries,
                    packageFolders,
                    references);
                if (failureDetail is not null)
                {
                    return failureDetail;
                }
            }
            else if (libraryType == "project")
            {
                ResolveProjectReference(
                    library.Name,
                    libraries,
                    projectDirectory,
                    targetFramework,
                    references,
                    droppedProjectReferences);
            }
        }

        return null;
    }

    private static string? ResolvePackageReference(
        string libraryKey,
        JsonElement libraryEntry,
        JsonElement libraries,
        IReadOnlyList<string> packageFolders,
        List<string> references)
    {
        if (!TryGetObject(libraryEntry, "compile", out var compile))
        {
            return null;
        }

        // Placeholder assets (`_._`) mark an intentionally empty compile group; a package whose
        // compile group is all placeholders contributes nothing and resolves no folder.
        var assetPaths = new List<string>();
        foreach (var asset in compile.EnumerateObject())
        {
            if (!asset.Name.EndsWith("_._", StringComparison.Ordinal))
            {
                assetPaths.Add(asset.Name);
            }
        }

        if (assetPaths.Count == 0)
        {
            return null;
        }

        if (libraries.ValueKind != JsonValueKind.Object ||
            !libraries.TryGetProperty(libraryKey, out var libraryDescription) ||
            !TryGetString(libraryDescription, "path", out var libraryPath))
        {
            return $"the package '{libraryKey}' has no library entry in obj\\project.assets.json; run dotnet restore";
        }

        // packageFolders resolve in declared order; the winner is the first folder carrying
        // NuGet's restore-success marker, so a folder holding a partially extracted package can
        // never win over a completed restore.
        var relativePackageDirectory = libraryPath.Replace('/', Path.DirectorySeparatorChar);
        string? packageDirectory = null;
        foreach (var packageFolder in packageFolders)
        {
            var candidate = Path.Combine(packageFolder, relativePackageDirectory);
            if (File.Exists(Path.Combine(candidate, ".nupkg.metadata")))
            {
                packageDirectory = candidate;
                break;
            }
        }

        if (packageDirectory is null)
        {
            return $"the package '{libraryKey}' has not been restored into any package folder; run dotnet restore";
        }

        foreach (var assetPath in assetPaths)
        {
            references.Add(
                Path.GetFullPath(
                    Path.Combine(
                        packageDirectory,
                        assetPath.Replace('/', Path.DirectorySeparatorChar))));
        }

        return null;
    }

    private static void ResolveProjectReference(
        string libraryKey,
        JsonElement libraries,
        string projectDirectory,
        string targetFramework,
        List<string> references,
        List<string> droppedProjectReferences)
    {
        var separator = libraryKey.IndexOf('/');
        var projectName = separator < 0 ? libraryKey : libraryKey[..separator];

        // Compiling the referenced project's sources is a recorded non-goal; the built output is
        // probed instead, and a missing output drops the reference while semantics stay active for
        // everything else.
        if (libraries.ValueKind != JsonValueKind.Object ||
            !libraries.TryGetProperty(libraryKey, out var libraryDescription) ||
            !TryGetString(libraryDescription, "path", out var relativeProjectPath))
        {
            droppedProjectReferences.Add(projectName);
            return;
        }

        var referencedProjectFile = Path.GetFullPath(
            Path.Combine(
                projectDirectory,
                relativeProjectPath.Replace('/', Path.DirectorySeparatorChar)));
        var referencedProjectDirectory =
            Path.GetDirectoryName(referencedProjectFile) ?? string.Empty;

        string? newestOutput = null;
        var newestStamp = default(DateTime);
        foreach (var configuration in (ReadOnlySpan<string>)["Debug", "Release"])
        {
            var candidate = Path.Combine(
                referencedProjectDirectory,
                "bin",
                configuration,
                targetFramework,
                projectName + ".dll");
            if (!File.Exists(candidate))
            {
                continue;
            }

            var stamp = File.GetLastWriteTimeUtc(candidate);
            if (newestOutput is null || stamp > newestStamp)
            {
                newestOutput = candidate;
                newestStamp = stamp;
            }
        }

        if (newestOutput is null)
        {
            droppedProjectReferences.Add(projectName);
        }
        else
        {
            references.Add(newestOutput);
        }
    }

    private static string? ResolveFrameworkReferences(
        JsonElement frameworkElement,
        string targetFramework,
        IReadOnlyList<string> packageFolders,
        List<string> references)
    {
        if (!TryGetObject(frameworkElement, "frameworkReferences", out var frameworkReferences))
        {
            return null;
        }

        foreach (var frameworkReference in frameworkReferences.EnumerateObject())
        {
            var failureDetail = string.Equals(
                frameworkReference.Name,
                NetCoreApplicationFrameworkName,
                StringComparison.OrdinalIgnoreCase)
                ? ResolveNetCoreApplicationReferences(
                    frameworkElement,
                    targetFramework,
                    references)
                : ResolveFrameworkReferencePack(
                    frameworkReference.Name,
                    frameworkElement,
                    targetFramework,
                    packageFolders,
                    references);
            if (failureDetail is not null)
            {
                return failureDetail;
            }
        }

        return null;
    }

    private static string? ResolveNetCoreApplicationReferences(
        JsonElement frameworkElement,
        string targetFramework,
        List<string> references)
    {
        if (!TryGetFrameworkVersion(targetFramework, out var frameworkVersion))
        {
            return $"the target framework '{targetFramework}' does not name a resolvable {NetCoreApplicationFrameworkName} version; run dotnet restore";
        }

        TryGetString(
            frameworkElement,
            "runtimeIdentifierGraphPath",
            out var runtimeIdentifierGraphPath);
        var dotnetRoot = ResolveDotnetRoot(
            runtimeIdentifierGraphPath,
            Environment.GetEnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        if (dotnetRoot is null)
        {
            return $"no .NET installation was found for the {NetCoreApplicationFrameworkName} reference assemblies; install the .NET SDK {frameworkVersion.Major}";
        }

        var packRootDirectory = Path.Combine(dotnetRoot, "packs", NetCoreApplicationTargetingPackName);
        if (!Directory.Exists(packRootDirectory))
        {
            return $"the {NetCoreApplicationTargetingPackName} targeting pack was not found under '{packRootDirectory}'; install the .NET SDK {frameworkVersion.Major}";
        }

        var packVersion = SelectTargetingPackVersion(packRootDirectory, frameworkVersion);
        var referenceDirectory = packVersion is null
            ? null
            : Path.Combine(packRootDirectory, packVersion, "ref", targetFramework);
        if (referenceDirectory is null || !Directory.Exists(referenceDirectory))
        {
            return $"the {NetCoreApplicationTargetingPackName} targeting pack has no {frameworkVersion.Major}.{frameworkVersion.Minor} reference assemblies under '{packRootDirectory}'; install the .NET SDK {frameworkVersion.Major}";
        }

        AppendReferenceAssemblies(referenceDirectory, references);
        return null;
    }

    private static string? ResolveFrameworkReferencePack(
        string frameworkReferenceName,
        JsonElement frameworkElement,
        string targetFramework,
        IReadOnlyList<string> packageFolders,
        List<string> references)
    {
        // A non-NETCore.App framework reference (the SDK's KnownFrameworkReference registration)
        // resolves from the restore's own pinned download of `<name>.Ref` — never from dotnet
        // packs, which know nothing about custom shared frameworks.
        var packName = frameworkReferenceName + ".Ref";
        string? pinnedVersion = null;
        if (frameworkElement.TryGetProperty("downloadDependencies", out var downloadDependencies) &&
            downloadDependencies.ValueKind == JsonValueKind.Array)
        {
            foreach (var dependency in downloadDependencies.EnumerateArray())
            {
                if (TryGetString(dependency, "name", out var dependencyName) &&
                    string.Equals(dependencyName, packName, StringComparison.OrdinalIgnoreCase) &&
                    TryGetString(dependency, "version", out var versionRange))
                {
                    pinnedVersion = ReadPinnedVersion(versionRange);
                    break;
                }
            }
        }

        if (pinnedVersion is null)
        {
            return $"the framework reference '{frameworkReferenceName}' has no pinned '{packName}' download dependency; run dotnet restore";
        }

        var relativeReferenceDirectory = Path.Combine(
            packName.ToLowerInvariant(),
            pinnedVersion.ToLowerInvariant(),
            "ref",
            targetFramework);
        foreach (var packageFolder in packageFolders)
        {
            var referenceDirectory = Path.Combine(packageFolder, relativeReferenceDirectory);
            if (Directory.Exists(referenceDirectory) &&
                AppendReferenceAssemblies(referenceDirectory, references))
            {
                return null;
            }
        }

        return $"the framework reference package '{packName}/{pinnedVersion}' has not been restored; run dotnet restore";
    }

    private static bool AppendReferenceAssemblies(
        string referenceDirectory,
        List<string> references)
    {
        var assemblies = Directory
            .EnumerateFiles(referenceDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        references.AddRange(assemblies);
        return assemblies.Length > 0;
    }

    private static string? SelectTargetingPackVersion(
        string packRootDirectory,
        Version frameworkVersion)
    {
        string? selectedName = null;
        Version? selectedVersion = null;
        var selectedIsStable = false;
        foreach (var versionDirectory in Directory.EnumerateDirectories(packRootDirectory))
        {
            var name = Path.GetFileName(versionDirectory);
            var prereleaseSeparator = name.IndexOf('-');
            var numericPart = prereleaseSeparator < 0 ? name : name[..prereleaseSeparator];
            if (!Version.TryParse(numericPart, out var version) ||
                version.Major != frameworkVersion.Major ||
                version.Minor != frameworkVersion.Minor)
            {
                continue;
            }

            // The highest version whose major.minor matches the target framework, with any stable
            // version preferred over every prerelease.
            var isStable = prereleaseSeparator < 0;
            if (selectedName is null ||
                (isStable && !selectedIsStable) ||
                (isStable == selectedIsStable &&
                 (version > selectedVersion ||
                  (version == selectedVersion &&
                   string.CompareOrdinal(name, selectedName) > 0))))
            {
                selectedName = name;
                selectedVersion = version;
                selectedIsStable = isStable;
            }
        }

        return selectedName;
    }

    private static (string RootNamespace, string ProjectDirectory, string? Configuration) ReadNames(
        string projectFilePath,
        string projectDirectory,
        string objDirectory)
    {
        string? rootNamespace = null;
        string? resolvedProjectDirectory = null;
        string? configuration = null;

        // The generator's own property channel: the newest generated editorconfig carries the
        // build's RootNamespace/ProjectDir/Configuration, so editor and build agree by
        // construction. Last assignment wins, matching editorconfig semantics.
        var editorConfigPath = FindNewestGeneratedEditorConfig(objDirectory);
        if (editorConfigPath is not null)
        {
            try
            {
                foreach (var line in File.ReadLines(editorConfigPath))
                {
                    if (TryReadBuildProperty(line, "RootNamespace", out var rootNamespaceValue))
                    {
                        rootNamespace = rootNamespaceValue ?? rootNamespace;
                    }
                    else if (TryReadBuildProperty(line, "ProjectDir", out var projectDirectoryValue))
                    {
                        resolvedProjectDirectory = projectDirectoryValue ?? resolvedProjectDirectory;
                    }
                    else if (TryReadBuildProperty(line, "Configuration", out var configurationValue))
                    {
                        configuration = configurationValue ?? configuration;
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // A transient read failure falls through to the project-file fallbacks below.
            }
        }

        rootNamespace ??= ReadLiteralRootNamespace(projectFilePath)
            ?? Path.GetFileNameWithoutExtension(projectFilePath);
        resolvedProjectDirectory ??=
            projectDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? projectDirectory
                : projectDirectory + Path.DirectorySeparatorChar;
        return (rootNamespace, resolvedProjectDirectory, configuration);
    }

    private static string? FindNewestGeneratedEditorConfig(string objDirectory)
    {
        if (!Directory.Exists(objDirectory))
        {
            return null;
        }

        string? newestPath = null;
        var newestStamp = default(DateTime);
        try
        {
            foreach (var candidate in Directory.EnumerateFiles(
                         objDirectory,
                         "*.GeneratedMSBuildEditorConfig.editorconfig",
                         SearchOption.AllDirectories))
            {
                var stamp = File.GetLastWriteTimeUtc(candidate);
                if (newestPath is null || stamp > newestStamp)
                {
                    newestPath = candidate;
                    newestStamp = stamp;
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return newestPath;
        }

        return newestPath;
    }

    private static bool TryReadBuildProperty(
        string line,
        string propertyName,
        out string? value)
    {
        value = null;
        var prefix = "build_property." + propertyName;
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = trimmed[prefix.Length..].TrimStart();
        if (!remainder.StartsWith('='))
        {
            return false;
        }

        var assigned = remainder[1..].Trim();
        value = assigned.Length == 0 ? null : assigned;
        return true;
    }

    private static string? ReadLiteralRootNamespace(string projectFilePath)
    {
        // The ViuUtilityStylesheetContext precedent: a deliberately non-evaluating project probe
        // that understands only literal assignments; the last literal wins, matching MSBuild's
        // document-ordered property evaluation.
        try
        {
            var project = XDocument.Load(projectFilePath, LoadOptions.None);
            string? rootNamespace = null;
            foreach (var element in project
                         .Descendants()
                         .Where(element => element.Name.LocalName == "RootNamespace"))
            {
                var value = element.Value.Trim();
                if (value.Length != 0 &&
                    !value.Contains("$(", StringComparison.Ordinal))
                {
                    rootNamespace = value;
                }
            }

            return rootNamespace;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            System.Xml.XmlException)
        {
            return null;
        }
    }

    private static (IReadOnlyList<string> SourceFilePaths, IReadOnlyList<string> ComponentFilePaths)
        EnumerateSourceCone(string projectDirectory)
    {
        // The SDK's default Compile glob (`**\*.cs` minus DefaultItemExcludes) and the Syntax
        // generator's AdditionalFiles glob (`**\*.viu;**\*.vue` minus the same): the excludes are
        // the project-root bin\ and obj\ output trees, which deliberately drops the obj-generated
        // AssemblyInfo — completion never needs it.
        var sourceFilePaths = new List<string>();
        var componentFilePaths = new List<string>();
        CollectSourceFiles(projectDirectory, "*.cs", sourceFilePaths);
        CollectSourceFiles(projectDirectory, "*.viu", componentFilePaths);
        CollectSourceFiles(projectDirectory, "*.vue", componentFilePaths);
        sourceFilePaths.Sort(StringComparer.OrdinalIgnoreCase);
        componentFilePaths.Sort(StringComparer.OrdinalIgnoreCase);
        return (sourceFilePaths, componentFilePaths);
    }

    private static void CollectSourceFiles(
        string projectDirectory,
        string searchPattern,
        List<string> results)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(
                         projectDirectory,
                         searchPattern,
                         SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(projectDirectory, file);
                if (IsUnderOutputDirectory(relativePath))
                {
                    continue;
                }

                results.Add(Path.GetFullPath(file));
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A partially enumerated cone degrades to fewer sibling documents, never to a failure.
        }
    }

    private static bool IsUnderOutputDirectory(string relativePath)
        => relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string ComputeCacheStamp(
        byte[] assetsBytes,
        string rootNamespace,
        string projectDirectory,
        string? configuration,
        IEnumerable<string> sourceFilePaths)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(assetsBytes);
        AppendText(hash, $"\n{rootNamespace}|{projectDirectory}|{configuration}\n");
        foreach (var filePath in sourceFilePaths)
        {
            long lastWriteTicks = 0;
            long length = 0;
            try
            {
                var information = new FileInfo(filePath);
                lastWriteTicks = information.LastWriteTimeUtc.Ticks;
                length = information.Length;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // A file deleted between enumeration and stat contributes a zero stamp; the next
                // request re-enumerates and the stamp settles.
            }

            AppendText(hash, $"{filePath}|{lastWriteTicks}|{length}\n");
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendText(IncrementalHash hash, string text)
        => hash.AppendData(Encoding.UTF8.GetBytes(text));

    private static IReadOnlyList<string> ReadPackageFolders(JsonElement root)
    {
        var packageFolders = new List<string>();
        if (TryGetObject(root, "packageFolders", out var packageFoldersElement))
        {
            foreach (var packageFolder in packageFoldersElement.EnumerateObject())
            {
                packageFolders.Add(packageFolder.Name);
            }
        }

        return packageFolders;
    }

    private static string? ReadPinnedVersion(string versionRange)
    {
        // A downloadDependencies pin is the exact range `[version, version]`; the lower bound is
        // the pinned version.
        var trimmed = versionRange.Trim().TrimStart('[', '(').TrimEnd(']', ')');
        var separator = trimmed.IndexOf(',');
        var version = (separator < 0 ? trimmed : trimmed[..separator]).Trim();
        return version.Length == 0 ? null : version;
    }

    private static bool TryGetFrameworkVersion(string targetFramework, out Version version)
    {
        version = new Version(0, 0);
        if (!targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Version.TryParse(targetFramework["net".Length..], out version!);
    }

    private static ViuProjectAssets CreateUnresolvable(string projectFilePath, string detail)
        => new()
        {
            Status = ViuProjectContextStatus.AssetsUnresolvable(projectFilePath, detail),
            ProjectFilePath = projectFilePath,
        };

    private static bool TryGetObject(
        JsonElement parent,
        string propertyName,
        out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetString(
        JsonElement parent,
        string propertyName,
        [NotNullWhen(true)] out string? value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }
}
