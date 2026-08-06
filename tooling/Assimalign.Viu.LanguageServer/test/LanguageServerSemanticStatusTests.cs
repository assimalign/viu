using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins the project-context status plumbing ([V01.01.12.23], #259): the host probes on document
/// open and on semantic feature requests, feeds the resolved context to
/// <see cref="IScriptSemanticLanguageService"/>, and reports each (project, status, detail) via
/// <c>window/logMessage</c> exactly once — never once per keystroke.
/// </summary>
public class LanguageServerSemanticStatusTests
{
    [Fact]
    public async Task RunAsync_MissingAssets_ReportsWarningOnceAcrossRequests()
    {
        var directory = CreateFixtureRoot("missing-assets");
        try
        {
            var projectFilePath = Path.Combine(directory, "Application.csproj");
            File.WriteAllText(
                projectFilePath,
                "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            var componentPath = Path.Combine(directory, "Card.viu");
            File.WriteAllText(componentPath, "@script\n");
            var documentUri = new Uri(componentPath).AbsoluteUri;

            await using var input = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    Frame(CreateOpenMessage(documentUri, "@script\n")) +
                    Frame(CreateCompletionMessage(documentUri, "first")) +
                    Frame(CreateCompletionMessage(documentUri, "second")) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","id":9,"method":"shutdown","params":null}
                        """) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","method":"exit"}
                        """)));
            await using var output = new MemoryStream();
            var host = new LanguageServerHost();

            var exitCode = await host.RunAsync(input, output);

            exitCode.ShouldBe(0);
            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            var logMessages = FindLogMessages(messages);
            // Once-only: the open and both completions observe the same (project, status, detail),
            // so exactly one notification is written, with the spec's message verbatim.
            logMessages.Count.ShouldBe(1);
            logMessages[0].GetProperty("type").GetInt32().ShouldBe(2);
            logMessages[0].GetProperty("message").GetString().ShouldBe(
                $"Viu semantic IntelliSense is unavailable for Application ('{projectFilePath}'): " +
                "obj\\project.assets.json has not been produced. Build or restore the project " +
                "once (dotnet build); features recover automatically.");
            FindResponse(messages, "first").TryGetProperty("result", out _).ShouldBeTrue();
            FindResponse(messages, "second").TryGetProperty("result", out _).ShouldBeTrue();

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ResolvableAssets_FeedsContextAndReportsActiveOnce()
    {
        var directory = CreateFixtureRoot("resolvable-assets");
        try
        {
            var projectFilePath = Path.Combine(directory, "Application.csproj");
            File.WriteAllText(projectFilePath, "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            File.WriteAllText(Path.Combine(directory, "Sibling.cs"), "// sibling source");
            var componentPath = Path.Combine(directory, "Card.viu");
            File.WriteAllText(componentPath, "@script\n");
            WriteMinimalResolvableAssetsFile(directory);
            File.SetLastWriteTimeUtc(projectFilePath, DateTime.UtcNow.AddMinutes(-10));
            var documentUri = new Uri(componentPath).AbsoluteUri;

            await using var input = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    Frame(CreateOpenMessage(documentUri, "@script\n")) +
                    Frame(CreateCompletionMessage(documentUri, "completion")) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","id":9,"method":"shutdown","params":null}
                        """) +
                    Frame(
                        """
                        {"jsonrpc":"2.0","method":"exit"}
                        """)));
            await using var output = new MemoryStream();
            var languageService = new ContextRecordingLanguageService();
            var host = new LanguageServerHost(languageService);

            var exitCode = await host.RunAsync(input, output);

            exitCode.ShouldBe(0);
            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            var logMessages = FindLogMessages(messages);
            logMessages.Count.ShouldBe(1);
            logMessages[0].GetProperty("type").GetInt32().ShouldBe(3);
            logMessages[0].GetProperty("message").GetString().ShouldBe(
                $"Viu semantic IntelliSense active for Application ('{projectFilePath}').");

            // The host fed the materialized context on the open and again on the completion.
            var configured = languageService.ConfiguredContexts;
            configured.Count.ShouldBe(2);
            foreach (var (configuredUri, context) in configured)
            {
                configuredUri.ShouldBe(documentUri);
                context.ShouldNotBeNull();
                context!.ProjectFilePath.ShouldBe(projectFilePath);
                context.RootNamespace.ShouldBe("Application");
                context.CacheStamp.ShouldNotBeNullOrEmpty();
                var sibling = context.SourceDocuments.ShouldHaveSingleItem();
                sibling.FilePath.ShouldBe(Path.Combine(directory, "Sibling.cs"));
                sibling.Text.ShouldBe("// sibling source");
                sibling.IsComponent.ShouldBeFalse();
            }

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateFixtureRoot(string scenario)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-semantic-status-tests",
            scenario + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WriteMinimalResolvableAssetsFile(string projectDirectory)
    {
        // The smallest resolvable restore: an empty RID-less target with no packages and no
        // framework references resolves to zero reference assemblies and activates semantics.
        Directory.CreateDirectory(Path.Combine(projectDirectory, "obj"));
        File.WriteAllText(
            Path.Combine(projectDirectory, "obj", "project.assets.json"),
            """
            {
              "version": 4,
              "targets": { "net10.0": {} },
              "libraries": {},
              "packageFolders": {},
              "project": { "version": "1.0.0", "frameworks": { "net10.0": {} } }
            }
            """);
    }

    private static string CreateOpenMessage(string documentUri, string text)
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
                        text,
                    },
                },
            });

    private static string CreateCompletionMessage(string documentUri, string identifier)
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
                        line = 0,
                        character = 0,
                    },
                },
            });

    private static List<JsonElement> FindLogMessages(List<JsonDocument> messages)
        => messages
            .Where(
                message =>
                    message.RootElement.TryGetProperty("method", out var method) &&
                    method.GetString() == "window/logMessage")
            .Select(message => message.RootElement.GetProperty("params"))
            .ToList();

    private static JsonElement FindResponse(List<JsonDocument> messages, string identifier)
        => messages.Single(
                message =>
                    message.RootElement.TryGetProperty("id", out var responseIdentifier) &&
                    responseIdentifier.ValueKind == JsonValueKind.String &&
                    responseIdentifier.GetString() == identifier)
            .RootElement;

    private static async Task<List<JsonDocument>> ReadAllMessagesAsync(Stream stream)
    {
        var reader = new LanguageServerProtocolMessageReader(stream);
        var messages = new List<JsonDocument>();
        while (true)
        {
            var message = await reader.ReadAsync();
            if (message is null)
            {
                return messages;
            }

            messages.Add(message);
        }
    }

    private static string Frame(string payload)
        => $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";

    /// <summary>
    /// A service that records every fed project context. The list is lock-guarded because the
    /// open-time feed runs on the loop thread while request-time feeds run on request tasks.
    /// </summary>
    private sealed class ContextRecordingLanguageService :
        IViuLanguageService,
        IScriptSemanticLanguageService
    {
        private readonly object synchronization = new();
        private readonly List<(string DocumentUri, LanguageProjectContext? Context)> configured = [];

        internal IReadOnlyList<(string DocumentUri, LanguageProjectContext? Context)> ConfiguredContexts
        {
            get
            {
                lock (synchronization)
                {
                    return configured.ToArray();
                }
            }
        }

        public void ConfigureProjectContext(string documentUri, LanguageProjectContext? context)
        {
            lock (synchronization)
            {
                configured.Add((documentUri, context));
            }
        }

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
