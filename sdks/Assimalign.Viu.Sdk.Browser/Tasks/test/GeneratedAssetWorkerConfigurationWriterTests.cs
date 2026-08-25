using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

using Assimalign.Viu.Sdk.Browser.Tasks;

namespace Assimalign.Viu.Sdk.Browser.Tasks.Tests;

public sealed class GeneratedAssetWorkerConfigurationWriterTests
{
    // [V01.01.12.30.04], #355 pins the version-1 generated-asset contract serialization.
    [Fact]
    public void Write_ValidGeneratedAsset_SerializesNormalizedGenericContract()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var outputPath = Path.Combine(directory, "generated", "..", "asset.css");
            var watchFilePath = Path.Combine(directory, "sources", "..", "source.input");
            var watchRootPath = Path.Combine(directory, "sources", ".");
            var manifestPath = Path.Combine(directory, "obj", "dependencies.manifest");
            var asset = CreateValidAsset(outputPath, watchFilePath);
            asset.SetMetadata("WatchRoots", watchRootPath);
            asset.SetMetadata("WatchExtensions", ".input;.INPUT");
            asset.SetMetadata("DependencyManifestPath", manifestPath);
            var configurationPath = Write(directory, asset);

            var lines = File.ReadAllLines(configurationPath);
            lines[0].ShouldBe(GeneratedAssetWorkerConfigurationWriter.Header);
            DecodeValues(lines, "identity").ShouldBe(
                new[] { Path.GetFullPath(outputPath) });
            DecodeValues(lines, "watch-file").ShouldBe(
                new[] { Path.GetFullPath(watchFilePath) });
            DecodeValues(lines, "watch-root").ShouldBe(
                new[] { Path.GetFullPath(watchRootPath) });
            DecodeValues(lines, "watch-extension").ShouldBe(new[] { ".input" });
            DecodeValues(lines, "dependency-manifest-path").ShouldBe(
                new[] { Path.GetFullPath(manifestPath) });
            DecodeValues(lines, "static-web-asset-path").ShouldBe(
                new[] { "wwwroot/generated.css" });
            DecodeValues(lines, "removal-behavior").ShouldBe(new[] { "Delete" });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_RelativeContractPath_RejectsProviderRegistration()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var asset = CreateValidAsset(
                "relative-output.css",
                Path.Combine(directory, "source.input"));

            Should.Throw<InvalidOperationException>(() => Write(directory, asset))
                .Message.ShouldContain("paths must be absolute");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_WatchRootWithoutExtensions_RejectsProviderRegistration()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var asset = CreateValidAsset(
                Path.Combine(directory, "asset.css"),
                Path.Combine(directory, "source.input"));
            asset.SetMetadata("WatchRoots", Path.Combine(directory, "sources"));
            asset.SetMetadata("WatchExtensions", string.Empty);

            Should.Throw<InvalidOperationException>(() => Write(directory, asset))
                .Message.ShouldContain("must declare WatchExtensions");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_UnstableStaticWebAssetRoute_RejectsProviderRegistration()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var asset = CreateValidAsset(
                Path.Combine(directory, "asset.css"),
                Path.Combine(directory, "source.input"));
            asset.SetMetadata("StaticWebAssetPath", "assets/generated.css");

            Should.Throw<InvalidOperationException>(() => Write(directory, asset))
                .Message.ShouldContain("beginning with wwwroot/");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_UnknownRemovalBehavior_RejectsProviderRegistration()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var asset = CreateValidAsset(
                Path.Combine(directory, "asset.css"),
                Path.Combine(directory, "source.input"));
            asset.SetMetadata("RemovalBehavior", "Retain");

            Should.Throw<InvalidOperationException>(() => Write(directory, asset))
                .Message.ShouldContain("Delete or PreserveEmpty");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static TaskItem CreateValidAsset(string outputPath, string watchFilePath)
    {
        var asset = new TaskItem(outputPath);
        asset.SetMetadata("WatchFiles", watchFilePath);
        asset.SetMetadata("RegenerationTarget", "GenerateAsset");
        asset.SetMetadata("StaticWebAssetPath", "wwwroot/generated.css");
        asset.SetMetadata("RemovalBehavior", "Delete");
        return asset;
    }

    private static string Write(string directory, ITaskItem asset)
    {
        var configurationPath = Path.Combine(directory, "obj", "worker.configuration");
        GeneratedAssetWorkerConfigurationWriter.Write(
            configurationPath,
            Path.Combine(directory, "Probe.proj"),
            directory,
            "dotnet",
            "Debug",
            "net10.0",
            string.Empty,
            Path.Combine(directory, "obj", "worker.state"),
            Path.Combine(directory, "obj", "worker.events"),
            Environment.ProcessId,
            100,
            new[] { asset },
            Array.Empty<ITaskItem>());
        return configurationPath;
    }

    private static string[] DecodeValues(IEnumerable<string> lines, string name) =>
        lines.Where(line => line.StartsWith(name + ":", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(
                Convert.FromBase64String(line.Substring(name.Length + 1))))
            .ToArray();

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-generated-asset-task-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
