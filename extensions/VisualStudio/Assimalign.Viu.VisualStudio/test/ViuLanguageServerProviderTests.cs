using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

public class ViuLanguageServerProviderTests
{
    [Fact]
    public void VueCompatibilityDocumentType_UsesRequiredCompatibilityIdentity()
    {
        var documentType =
            ViuLanguageServerProvider.VueCompatibilityDocumentType;

        documentType.Name.ShouldBe("viu-vue");
        documentType.FileExtensions.ShouldBe([".vue"]);
    }

    /// <summary>
    /// Every contributed part applies to both container document types. The <c>.vue</c> container is
    /// a declared external compatibility target ([V01.01.06.09]) served by the same language server,
    /// so a tagger registered only for <c>viu</c> left <c>.vue</c> documents with no colorization.
    /// </summary>
    /// <remarks>
    /// The assertion reads the generated manifest rather than the configuration properties: the
    /// contribution generator evaluates those properties at compile time, and an
    /// <c>ExtensionPart</c> cannot be constructed without a live extension host.
    /// </remarks>
    [Fact]
    public void GeneratedManifest_RegistersEveryPartForBothContainerDocumentTypes()
    {
        (var documentTypesByPart, var patternsByPart) = ReadAppliesToFromGeneratedManifest();

        string taggerProvider = typeof(ViuClassificationTaggerProvider).FullName!;
        string languageServerProvider = typeof(ViuLanguageServerProvider).FullName!;

        documentTypesByPart[taggerProvider].Distinct().ShouldBe(["viu", "viu-vue"], ignoreOrder: true);
        documentTypesByPart[languageServerProvider].Distinct().ShouldBe(["viu", "viu-vue"], ignoreOrder: true);

        // A language server may only be filtered by document type - the contribution generator fails
        // the build on a glob filter there - so the server carries no path patterns.
        patternsByPart[languageServerProvider].ShouldBeEmpty();
    }

    /// <summary>
    /// Classification must not depend on the container content types having materialized. Nothing
    /// static registers the <c>.viu</c> extension, so that binding is created only at runtime when the
    /// extension applies its <c>documentTypes</c>; a document opened before then never matches a
    /// document-type filter. The tagger therefore also matches on document file path.
    /// </summary>
    [Fact]
    public void GeneratedManifest_MatchesClassificationOnFilePathAsWellAsDocumentType()
    {
        (_, var patternsByPart) = ReadAppliesToFromGeneratedManifest();

        patternsByPart[typeof(ViuClassificationTaggerProvider).FullName!]
            .Distinct()
            .ShouldBe(["**/*.viu", "**/*.vue"], ignoreOrder: true);
    }

    /// <summary>
    /// Reads every contributed part's <c>appliesTo</c> filters out of the generated manifest, split
    /// into document-type filters and glob-pattern filters, keyed by the part's service moniker.
    /// </summary>
    private static (Dictionary<string, List<string>> DocumentTypes, Dictionary<string, List<string>> Patterns)
        ReadAppliesToFromGeneratedManifest()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(FindGeneratedManifest()));

        Dictionary<string, List<string>> documentTypesByPart = [];
        Dictionary<string, List<string>> patternsByPart = [];

        foreach (JsonElement part in manifest.RootElement.GetProperty("parts").EnumerateArray())
        {
            string name = part.GetProperty("serviceMoniker").GetProperty("name").GetString()!;
            if (!documentTypesByPart.ContainsKey(name))
            {
                documentTypesByPart[name] = [];
                patternsByPart[name] = [];
            }

            foreach (JsonElement metadata in part.GetProperty("metadata").EnumerateArray())
            {
                if (!metadata.GetProperty("values").TryGetProperty("appliesTo", out JsonElement appliesTo))
                {
                    continue;
                }

                foreach (JsonElement filter in appliesTo.EnumerateArray())
                {
                    if (filter.TryGetProperty("documentType", out JsonElement documentType))
                    {
                        documentTypesByPart[name].Add(documentType.GetString()!);
                    }
                    else if (filter.TryGetProperty("pattern", out JsonElement pattern))
                    {
                        patternsByPart[name].Add(pattern.GetString()!);
                    }
                }
            }
        }

        return (documentTypesByPart, patternsByPart);
    }

    /// <summary>
    /// Locates the contribution manifest emitted by the shipping project, which the test project
    /// builds as a dependency but does not copy into its own output.
    /// </summary>
    private static string FindGeneratedManifest()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && directory.Name != "test")
        {
            directory = directory.Parent;
        }

        DirectoryInfo projectDirectory = directory?.Parent
            ?? throw new DirectoryNotFoundException(
                $"No test project directory above '{AppContext.BaseDirectory}'.");

        string manifest = Directory
            .EnumerateFiles(
                Path.Combine(projectDirectory.FullName, "src"),
                "extension.json",
                SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == ".vsextension")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new FileNotFoundException(
                $"No generated extension.json under '{projectDirectory.FullName}\\src'.");

        return manifest;
    }
}
