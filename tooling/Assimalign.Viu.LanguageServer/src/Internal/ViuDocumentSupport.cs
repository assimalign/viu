using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Assimalign.Viu.LanguageServer;

internal static class ViuDocumentSupport
{
    private const string ViuSoftwareDevelopmentKitName = "Assimalign.Viu.Sdk";
    private const string ViuBrowserSoftwareDevelopmentKitName =
        "Assimalign.Viu.Sdk.Browser";

    internal static bool IsSupported(string documentUri)
    {
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) ||
            !uri.IsFile)
        {
            return documentUri.EndsWith(
                ".viu",
                StringComparison.OrdinalIgnoreCase);
        }

        var extension = Path.GetExtension(uri.LocalPath);
        if (string.Equals(extension, ".viu", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(extension, ".vue", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryFindOwningViuProject(uri.LocalPath, out _);
    }

    internal static bool TryFindOwningViuProject(
        string documentPath,
        out string? projectPath)
    {
        projectPath = null;
        var directory = Path.GetDirectoryName(documentPath);
        while (!string.IsNullOrEmpty(directory))
        {
            string[] projectFiles;
            try
            {
                projectFiles = Directory
                    .EnumerateFiles(
                        directory,
                        "*.csproj",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            // The closest directory containing a project establishes ownership. Do not walk past an
            // unrelated nested project and accidentally activate Viu features from an ancestor.
            if (projectFiles.Length != 0)
            {
                var viuProjectFiles = projectFiles
                    .Where(UsesViuSoftwareDevelopmentKit)
                    .ToArray();

                // A path shared by both Viu and non-Viu project files has no reliable physical
                // owner without an evaluated Visual Studio project-system query. Fail closed so
                // one Viu project cannot claim its collocated Vue project's documents.
                if (viuProjectFiles.Length != projectFiles.Length)
                {
                    return false;
                }

                projectPath = viuProjectFiles.FirstOrDefault();
                return projectPath is not null;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return false;
    }

    private static bool UsesViuSoftwareDevelopmentKit(string projectPath)
    {
        try
        {
            var document = XDocument.Load(projectPath, LoadOptions.None);
            var project = document.Root;
            if (project is null)
            {
                return false;
            }

            bool? explicitLanguageServiceSetting = null;
            foreach (var element in project
                         .Descendants()
                         .Where(
                             element =>
                                 element.Name.LocalName ==
                                 "ViuVisualStudioLanguageServiceEnabled"))
            {
                if (bool.TryParse(
                        element.Value.Trim(),
                        out var enabled))
                {
                    // Project properties are document ordered. Retain the last literal assignment
                    // that this deliberately non-evaluating project probe can understand.
                    explicitLanguageServiceSetting = enabled;
                }
            }

            if (explicitLanguageServiceSetting.HasValue)
            {
                return explicitLanguageServiceSetting.Value;
            }

            if (ContainsViuSoftwareDevelopmentKit(
                    project.Attribute("Sdk")?.Value))
            {
                return true;
            }

            foreach (var softwareDevelopmentKit in project
                         .Descendants()
                         .Where(element => element.Name.LocalName == "Sdk"))
            {
                if (ContainsViuSoftwareDevelopmentKit(
                        softwareDevelopmentKit.Attribute("Name")?.Value))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            System.Xml.XmlException)
        {
            return false;
        }
    }

    private static bool ContainsViuSoftwareDevelopmentKit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var item in value.Split(';'))
        {
            var name = item.Trim();
            if (MatchesSoftwareDevelopmentKit(
                    name,
                    ViuSoftwareDevelopmentKitName) ||
                MatchesSoftwareDevelopmentKit(
                    name,
                    ViuBrowserSoftwareDevelopmentKitName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSoftwareDevelopmentKit(
        string value,
        string softwareDevelopmentKitName)
    {
        return string.Equals(
                value,
                softwareDevelopmentKitName,
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                softwareDevelopmentKitName + "/",
                StringComparison.OrdinalIgnoreCase);
    }
}
