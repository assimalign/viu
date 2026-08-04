using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.Tooling.LanguageServer;

internal static class ViuUtilityStylesheetContext
{
    internal static ViuUtilityStylesheetConfiguration? ReadForDocument(
        string documentUri)
    {
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) ||
            !uri.IsFile ||
            !ViuDocumentSupport.TryFindOwningViuProject(
                uri.LocalPath,
                out var projectPath) ||
            projectPath is null)
        {
            return null;
        }

        try
        {
            var project = XDocument.Load(projectPath, LoadOptions.None);
            var includes = project
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName == "ViuUtilityCss")
                .Select(
                    element =>
                        element.Attribute("Include")?.Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (includes.Length != 1 ||
                ContainsUnresolvedBuildSyntax(includes[0]))
            {
                return null;
            }

            var projectDirectory =
                Path.GetDirectoryName(projectPath) ?? string.Empty;
            var stylesheetPath = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    includes[0].Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!File.Exists(stylesheetPath))
            {
                return null;
            }

            var stylesheetText = File.ReadAllText(stylesheetPath);
            var referenceGraph =
                UtilityStylesheetReferenceGraphBuilder.Build(
                    stylesheetPath,
                    stylesheetText,
                    ResolveReference);
            return new ViuUtilityStylesheetConfiguration(
                stylesheetText,
                stylesheetPath,
                referenceGraph);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            System.Xml.XmlException or ArgumentException or
            NotSupportedException)
        {
            return null;
        }
    }

    private static bool ContainsUnresolvedBuildSyntax(string include)
        => include.Contains("$(", StringComparison.Ordinal) ||
           include.IndexOfAny(['*', '?']) >= 0;

    private static UtilityStylesheetReference? ResolveReference(
        string referencingSourceIdentity,
        string specifier)
    {
        try
        {
            var referencingDirectory =
                Path.GetDirectoryName(referencingSourceIdentity) ??
                string.Empty;
            var resolvedPath = Path.GetFullPath(
                Path.Combine(
                    referencingDirectory,
                    specifier.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            return File.Exists(resolvedPath)
                ? new UtilityStylesheetReference(
                    referencingSourceIdentity,
                    specifier,
                    resolvedPath,
                    File.ReadAllText(resolvedPath))
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
