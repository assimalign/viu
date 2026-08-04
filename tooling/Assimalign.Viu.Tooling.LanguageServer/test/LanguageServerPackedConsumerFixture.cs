using System;
using System.IO;
using System.Text.Json;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// The lane-supplied inputs for the packed-consumer round trip
/// (<see cref="LanguageServerPackedConsumerTests"/>): the published single-file server
/// executable, the repo-local package feed, and the Viu SDK version to pin. Read from the JSON
/// file named by the <c>VIU_PACKED_CONSUMER_FIXTURE</c> environment variable, which
/// <c>scripts\Test-LanguageServerSemanticRoundTrip.ps1</c> writes. An absent variable yields
/// <see langword="null"/> and the test stays inert, keeping every ordinary <c>dotnet test</c>
/// invocation hermetic, offline, and fast; a present but broken fixture fails loudly.
/// </summary>
internal sealed record LanguageServerPackedConsumerFixture(
    string ServerExecutable,
    string PackageFeed,
    string SdkVersion)
{
    internal static LanguageServerPackedConsumerFixture? TryReadFromEnvironment()
    {
        var fixturePath = Environment.GetEnvironmentVariable("VIU_PACKED_CONSUMER_FIXTURE");
        if (string.IsNullOrEmpty(fixturePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(fixturePath));
        var root = document.RootElement;
        var fixture = new LanguageServerPackedConsumerFixture(
            root.GetProperty("serverExecutable").GetString()!,
            root.GetProperty("packageFeed").GetString()!,
            root.GetProperty("sdkVersion").GetString()!);
        if (!File.Exists(fixture.ServerExecutable))
        {
            throw new FileNotFoundException(
                "The packed-consumer fixture names a published server executable that does not " +
                "exist; run extensions\\VisualStudio\\Build.ps1 first.",
                fixture.ServerExecutable);
        }

        if (!Directory.Exists(fixture.PackageFeed))
        {
            throw new DirectoryNotFoundException(
                $"The packed-consumer fixture names a package feed that does not exist " +
                $"('{fixture.PackageFeed}'); run scripts/Install-Local.ps1 first.");
        }

        return fixture;
    }
}
