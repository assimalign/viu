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
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(FindGeneratedManifest()));

        JsonElement.ArrayEnumerator parts = manifest.RootElement.GetProperty("parts").EnumerateArray();
        Dictionary<string, List<string>> documentTypesByPart = [];

        foreach (JsonElement part in parts)
        {
            string name = part.GetProperty("serviceMoniker").GetProperty("name").GetString()!;
            List<string> documentTypes = documentTypesByPart.TryGetValue(name, out List<string>? existing)
                ? existing
                : documentTypesByPart[name] = [];

            foreach (JsonElement metadata in part.GetProperty("metadata").EnumerateArray())
            {
                if (!metadata.GetProperty("values").TryGetProperty("appliesTo", out JsonElement appliesTo))
                {
                    continue;
                }

                foreach (JsonElement filter in appliesTo.EnumerateArray())
                {
                    documentTypes.Add(filter.GetProperty("documentType").GetString()!);
                }
            }
        }

        string taggerProvider = typeof(ViuClassificationTaggerProvider).FullName!;
        string languageServerProvider = typeof(ViuLanguageServerProvider).FullName!;

        documentTypesByPart[taggerProvider].Distinct().ShouldBe(["viu", "viu-vue"], ignoreOrder: true);
        documentTypesByPart[languageServerProvider].Distinct().ShouldBe(["viu", "viu-vue"], ignoreOrder: true);
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
