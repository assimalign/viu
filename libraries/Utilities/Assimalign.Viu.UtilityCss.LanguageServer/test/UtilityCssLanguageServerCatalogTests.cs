using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.LanguageServer.Tests;

public sealed class UtilityCssLanguageServerCatalogTests
{
    [Fact]
    public async Task RunAsync_CatalogOnlyCompletion_PreservesColorAndReportsCatalogTruncation()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "viu-utility-css-language-server-catalog-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);
        try
        {
            var projectPath = Path.Combine(projectDirectory, "Application.csproj");
            var documentPath = Path.Combine(projectDirectory, "Index.html");
            var sidecarDirectory = Path.Combine(
                projectDirectory,
                "obj",
                "Debug",
                "net10.0",
                "utilitycss");
            var manifestPath = Path.Combine(
                sidecarDirectory,
                "utilitycss.manifest.v1.json");
            var catalogPath = Path.Combine(
                sidecarDirectory,
                "utilitycss.classcatalog.v1.json");
            Directory.CreateDirectory(sidecarDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            var manifest = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["engineVersion"] = "1.0.0.0",
                ["entryStylesheetPath"] = null,
                ["sourceFiles"] = new JsonArray(),
                ["themeContentHash"] = new string('0', 64),
                ["bundle"] = new JsonObject
                {
                    ["path"] = Path.Combine(
                        sidecarDirectory,
                        "Application.utilities.css"),
                    ["name"] = "Application.utilities.css",
                },
            };
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            const string Candidate = "bg-[#123456]";
            var catalog = new JsonObject
            {
                ["version"] = 1,
                ["truncated"] = true,
                ["entries"] = new JsonArray
                {
                    (JsonNode)new JsonObject
                    {
                        ["class"] = Candidate,
                        ["css"] = ".bg-\\[\\#123456\\] { background-color: #123456; }",
                        ["colorValue"] = "#123456",
                    },
                },
            };
            File.WriteAllText(catalogPath, catalog.ToJsonString());

            const string Prefix = "bg-[#12";
            const string Source = "<div class=\"bg-[#12\"></div>\n";
            File.WriteAllText(documentPath, Source);
            var cursor = Source.IndexOf(Prefix, StringComparison.Ordinal) +
                Prefix.Length;
            var documentUri = new Uri(documentPath).AbsoluteUri;

            var run = await UtilityCssLanguageServerTestProtocol.RunAsync(
                UtilityCssLanguageServerTestProtocol.InitializeRequest("initialize"),
                UtilityCssLanguageServerTestProtocol.InitializedNotification(),
                UtilityCssLanguageServerTestProtocol.DidOpenNotification(
                    documentUri,
                    "html",
                    Source),
                UtilityCssLanguageServerTestProtocol.CompletionRequest(
                    "completion",
                    documentUri,
                    Source,
                    cursor),
                UtilityCssLanguageServerTestProtocol.ShutdownRequest("shutdown"),
                UtilityCssLanguageServerTestProtocol.ExitNotification());
            try
            {
                run.ExitCode.ShouldBe(0);
                var completion = UtilityCssLanguageServerTestProtocol.FindResponse(
                        run.Messages,
                        "completion")
                    .GetProperty("result");
                completion.GetProperty("isIncomplete").GetBoolean().ShouldBeTrue();
                var item = UtilityCssLanguageServerTestProtocol.FindCompletionItem(
                    completion.GetProperty("items"),
                    Candidate);
                item.GetProperty("data")
                    .GetProperty("colorValue")
                    .GetString()
                    .ShouldBe("#123456");
            }
            finally
            {
                UtilityCssLanguageServerTestProtocol.DisposeMessages(run.Messages);
            }
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }
}
