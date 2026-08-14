using System;
using System.IO;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins host-side class-catalog discovery independently of catalog parsing: any project-containing
/// directory can contribute files, one newest file per distinct filename is selected, deterministic
/// ordering is preserved, and unchanged selected file state reuses the configuration identity
/// ([V01.01.12.30], #346).
/// </summary>
public class ViuClassCatalogReaderTests
{
    [Fact]
    public void Read_SharedProjectDirectoryObjCatalogs_SelectsNewestAndReusesUnchangedConfiguration()
    {
        var directory = CreateFixtureRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Application.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(
                Path.Combine(directory, "Library.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            var documentPath = Path.Combine(directory, "Card.viu");
            File.WriteAllText(documentPath, "<template />");

            var alphaPath = Path.Combine(
                directory,
                "obj",
                "alpha",
                "alpha.classcatalog.v1.json");
            var olderProviderPath = Path.Combine(
                directory,
                "obj",
                "older",
                "provider.classcatalog.v1.json");
            var newerProviderPath = Path.Combine(
                directory,
                "obj",
                "newer",
                "provider.classcatalog.v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(alphaPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(olderProviderPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(newerProviderPath)!);

            const string alphaJson = "{\"version\":1,\"entries\":[],\"truncated\":false}";
            const string olderJson = "{\"version\":1,\"entries\":[{\"class\":\"older\",\"css\":\".older{}\"}],\"truncated\":false}";
            const string newerJson = "{\"version\":1,\"entries\":[{\"class\":\"newer\",\"css\":\".newer{}\"}],\"truncated\":false}";
            File.WriteAllText(alphaPath, alphaJson);
            File.WriteAllText(olderProviderPath, olderJson);
            File.WriteAllText(newerProviderPath, newerJson);
            var baseline = DateTime.UtcNow.AddMinutes(-10);
            File.SetLastWriteTimeUtc(alphaPath, baseline);
            File.SetLastWriteTimeUtc(olderProviderPath, baseline.AddMinutes(1));
            File.SetLastWriteTimeUtc(newerProviderPath, baseline.AddMinutes(2));

            var reader = new ViuClassCatalogReader();
            var documentUri = new Uri(documentPath).AbsoluteUri;

            var first = reader.Read(documentUri);
            var unchanged = reader.Read(documentUri);

            first.ShouldNotBeNull();
            ReferenceEquals(first, unchanged).ShouldBeTrue();
            first!.CatalogJsonDocuments.ShouldBe([alphaJson, newerJson]);

            const string refreshedJson = "{\"version\":1,\"entries\":[{\"class\":\"refreshed\",\"css\":\".refreshed{display:grid}\"}],\"truncated\":false}";
            File.WriteAllText(newerProviderPath, refreshedJson);
            File.SetLastWriteTimeUtc(newerProviderPath, baseline.AddMinutes(3));

            var refreshed = reader.Read(documentUri);

            refreshed.ShouldNotBeNull();
            ReferenceEquals(first, refreshed).ShouldBeFalse();
            refreshed!.CatalogJsonDocuments.ShouldBe([alphaJson, refreshedJson]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateFixtureRoot()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-class-catalog-reader-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
