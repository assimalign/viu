using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins the complete host boundary for generic build-contributed class catalogs: a plain project
/// is discovered after open, color metadata crosses the protocol, authored component classes keep
/// precedence, changed files refresh on the next request, and removal restores the baseline
/// ([V01.01.12.30], #346).
/// </summary>
public class LanguageServerClassCatalogTests
{
    [Fact]
    public void FeatureConfiguration_ContextOnlyGeneration_DoesNotSuppressCatalogSnapshot()
    {
        var state = new LanguageServerDocumentPublicationState();
        var catalogFeature = state.BeginFeatureConfiguration(
            includesProjectContext: true,
            includesClassCatalog: true);
        var contextOnlyFeature = state.BeginFeatureConfiguration(
            includesProjectContext: true,
            includesClassCatalog: false);
        var appliedProjectContext = string.Empty;
        var appliedClassCatalog = string.Empty;

        state.TryApplyFeatureRequest(
                contextOnlyFeature.ProjectContext,
                () => appliedProjectContext = "newer context",
                contextOnlyFeature.ClassCatalog,
                applyClassCatalog: null,
                CancellationToken.None)
            .ShouldBeTrue();
        state.TryApplyFeatureRequest(
                catalogFeature.ProjectContext,
                () => appliedProjectContext = "older context",
                catalogFeature.ClassCatalog,
                () => appliedClassCatalog = "catalog",
                CancellationToken.None)
            .ShouldBeTrue();

        appliedProjectContext.ShouldBe("newer context");
        appliedClassCatalog.ShouldBe("catalog");
    }

    [Fact]
    public async Task RunAsync_CatalogOnlyService_ReceivesConfigurationBeforeCompletion()
    {
        const string source =
            "<template>\n" +
            "<div class=\"catalog-\"></div>\n" +
            "</template>";
        var directory = CreateFixtureRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Application.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            var documentPath = Path.Combine(directory, "Card.viu");
            File.WriteAllText(documentPath, source);
            var catalogPath = Path.Combine(
                directory,
                "obj",
                "provider.classcatalog.v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
            const string catalogJson =
                "{\"version\":1,\"entries\":[],\"truncated\":false}";
            File.WriteAllText(catalogPath, catalogJson);
            var documentUri = new Uri(documentPath).AbsoluteUri;
            var languageService = new CatalogRecordingLanguageService();

            await using var session = new LanguageServerHostSession(
                new LanguageServerHost(languageService));
            await session.SendAsync(CreateOpenMessage(documentUri, source));
            await session.SendAsync(
                CreateCompletionMessage(
                    documentUri,
                    "completion",
                    source.Split('\n')[1].IndexOf("catalog-", StringComparison.Ordinal) +
                    "catalog-".Length));
            using (await session.ReadResponseAsync("completion"))
            {
            }

            languageService.Configuration.ShouldNotBeNull();
            languageService.Configuration!.CatalogJsonDocuments.ShouldBe([catalogJson]);

            await session.SendAsync(
                """
                {"jsonrpc":"2.0","id":"shutdown","method":"shutdown","params":null}
                """);
            using (await session.ReadResponseAsync("shutdown"))
            {
            }

            await session.SendAsync(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """);
            (await session.CompleteAsync()).ShouldBe(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ObjCatalogAddedChangedAndRemoved_RefreshesCompletionSnapshot()
    {
        const string source =
            "<template>\n" +
            "  <div class=\"brand-\"></div>\n" +
            "</template>\n" +
            "<style>\n" +
            ".brand-owned { color: red; }\n" +
            "</style>\n";
        var directory = CreateFixtureRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Application.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            var documentPath = Path.Combine(directory, "Card.viu");
            File.WriteAllText(documentPath, source);
            var documentUri = new Uri(documentPath).AbsoluteUri;
            var classValuePosition = source.Split('\n')[1]
                .IndexOf("brand-", StringComparison.Ordinal) + "brand-".Length;

            await using var session = new LanguageServerHostSession(
                new LanguageServerHost());
            await session.SendAsync(CreateOpenMessage(documentUri, source));

            await session.SendAsync(
                CreateCompletionMessage(documentUri, "baseline", classValuePosition));
            List<string> baselineLabels;
            using (var baselineResponse = await session.ReadResponseAsync("baseline"))
            {
                var baselineResult = baselineResponse.RootElement.GetProperty("result");
                baselineResult.GetProperty("isIncomplete").GetBoolean().ShouldBeFalse();
                baselineLabels = GetLabels(baselineResult.GetProperty("items"));
                baselineLabels.ShouldBe(["brand-owned"]);
            }

            var catalogPath = Path.Combine(
                directory,
                "obj",
                "Debug",
                "net10.0",
                "provider.classcatalog.v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
            File.WriteAllText(
                catalogPath,
                """
                {
                  "version": 1,
                  "entries": [
                    {
                      "class": "brand-owned",
                      "css": ".brand-owned { color: blue; }",
                      "colorValue": "#0000ff"
                    },
                    {
                      "class": "brand-catalog",
                      "css": ".brand-catalog { color: #123456; }",
                      "colorValue": "#123456"
                    }
                  ],
                  "truncated": true
                }
                """);
            File.SetLastWriteTimeUtc(catalogPath, DateTime.UtcNow.AddMinutes(1));

            await session.SendAsync(
                CreateCompletionMessage(documentUri, "catalog", classValuePosition));
            using (var catalogResponse = await session.ReadResponseAsync("catalog"))
            {
                var result = catalogResponse.RootElement.GetProperty("result");
                result.GetProperty("isIncomplete").GetBoolean().ShouldBeTrue();
                var items = result.GetProperty("items");
                GetLabels(items).ShouldBe(["brand-owned", "brand-catalog"]);

                var authored = FindItem(items, "brand-owned");
                authored.GetProperty("kind").GetInt32().ShouldBe(10);
                authored.GetProperty("detail").GetString().ShouldBe("Component style class");
                authored.GetProperty("data").TryGetProperty("colorValue", out _).ShouldBeFalse();

                var contributed = FindItem(items, "brand-catalog");
                contributed.GetProperty("kind").GetInt32().ShouldBe(16);
                contributed.GetProperty("data").GetProperty("colorValue").GetString()
                    .ShouldBe("#123456");
            }

            File.WriteAllText(
                catalogPath,
                """
                {
                  "version": 1,
                  "entries": [
                    {
                      "class": "brand-refreshed",
                      "css": ".brand-refreshed { color: #abcdef; }",
                      "colorValue": "#abcdef"
                    }
                  ],
                  "truncated": false
                }
                """);
            File.SetLastWriteTimeUtc(catalogPath, DateTime.UtcNow.AddMinutes(2));

            await session.SendAsync(
                CreateCompletionMessage(documentUri, "refreshed", classValuePosition));
            using (var refreshedResponse = await session.ReadResponseAsync("refreshed"))
            {
                var result = refreshedResponse.RootElement.GetProperty("result");
                result.GetProperty("isIncomplete").GetBoolean().ShouldBeFalse();
                GetLabels(result.GetProperty("items"))
                    .ShouldBe(["brand-owned", "brand-refreshed"]);
            }

            File.Delete(catalogPath);

            await session.SendAsync(
                CreateCompletionMessage(documentUri, "removed", classValuePosition));
            using (var removedResponse = await session.ReadResponseAsync("removed"))
            {
                var result = removedResponse.RootElement.GetProperty("result");
                result.GetProperty("isIncomplete").GetBoolean().ShouldBeFalse();
                GetLabels(result.GetProperty("items")).ShouldBe(baselineLabels);
            }

            await session.SendAsync(
                """
                {"jsonrpc":"2.0","id":"shutdown","method":"shutdown","params":null}
                """);
            using (var shutdown = await session.ReadResponseAsync("shutdown"))
            {
                shutdown.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
            }

            await session.SendAsync(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """);
            (await session.CompleteAsync()).ShouldBe(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonElement FindItem(JsonElement items, string label)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.GetProperty("label").GetString() == label)
            {
                return item;
            }
        }

        throw new InvalidOperationException($"Completion item '{label}' was not found.");
    }

    private static List<string> GetLabels(JsonElement items)
    {
        var labels = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            labels.Add(item.GetProperty("label").GetString()!);
        }

        return labels;
    }

    private static string CreateOpenMessage(string documentUri, string source)
        => JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didOpen",
                @params = new
                {
                    textDocument = new
                    {
                        uri = documentUri,
                        languageId = "viu",
                        version = 1,
                        text = source,
                    },
                },
            });

    private static string CreateCompletionMessage(
        string documentUri,
        string identifier,
        int character)
        => JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = identifier,
                method = "textDocument/completion",
                @params = new
                {
                    textDocument = new
                    {
                        uri = documentUri,
                    },
                    position = new
                    {
                        line = 1,
                        character,
                    },
                },
            });

    private static string CreateFixtureRoot()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-class-catalog-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class CatalogRecordingLanguageService :
        ILanguageService,
        IClassCatalogLanguageService
    {
        internal LanguageClassCatalogConfiguration? Configuration { get; private set; }

        public void ConfigureClassCatalogs(
            string documentUri,
            LanguageClassCatalogConfiguration? configuration)
            => Configuration = configuration;

        public void OpenDocument(string documentUri, string text, int? version)
        {
        }

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes)
            => true;

        public bool CloseDocument(string documentUri) => true;

        public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
            string documentUri,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageDiagnostic>();

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageCompletionItem>();

        public LanguageHover? GetHover(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            => null;

        public string? ResolveCompletionDocumentation(
            string documentUri,
            string completionLabel,
            CancellationToken cancellationToken = default)
            => null;

        public IReadOnlyList<LanguageDocumentSymbol> GetDocumentSymbols(
            string documentUri,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageDocumentSymbol>();

        public IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(
            string documentUri,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageFoldingRange>();

        public IReadOnlyList<LanguageCodeAction> GetCodeActions(
            string documentUri,
            LanguageRange range,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageCodeAction>();
    }
}
