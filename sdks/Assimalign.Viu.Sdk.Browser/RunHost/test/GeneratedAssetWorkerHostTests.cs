using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Sdk.Browser.RunHost.Tests;

public sealed class GeneratedAssetWorkerHostTests
{
    [Fact]
    public void ReadManagedStylesheetPaths_ValidConfiguration_ReturnsDistinctNormalizedCssRoutes()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "viu-run-host-configuration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        string configurationFilePath = Path.Combine(
            directoryPath,
            "worker.configuration");
        try
        {
            File.WriteAllLines(
                configurationFilePath,
                [
                    "viu-generated-asset-worker-configuration-v1",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/component.css"),
                    "asset-end",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/scripts/generated.js"),
                    "asset-end",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/utilities.css"),
                    "asset-end",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/component.css"),
                    "asset-end",
                ],
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            IReadOnlyList<string> paths =
                GeneratedAssetWorkerHost.ReadManagedStylesheetPaths(
                    configurationFilePath);

            paths.ShouldBe(["/component.css", "/utilities.css"]);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ReadManagedStylesheetPaths_UnsupportedHeader_ThrowsInvalidDataException()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                "unsupported",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Action read = () =>
                GeneratedAssetWorkerHost.ReadManagedStylesheetPaths(path);

            read.ShouldThrow<InvalidDataException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Encode(string name, string value) =>
        name + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
