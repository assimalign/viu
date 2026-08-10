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
/// Pins the opt-in server notification that preserves exact C# editor classification names for the
/// thin Visual Studio adapter. [V01.01.12.07.11]
/// </summary>
public class LanguageServerSemanticClassificationPublicationTests
{
    [Fact]
    public async Task RunAsync_OptedInClient_PublishesExactVersionedNamesAndClose()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"initializationOptions":{"semanticClassificationNotifications":true},"capabilities":{}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\n    int value;\n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///Card.viu","version":2},"contentChanges":[{"text":"@script {\n    int changed;\n}\n"}]}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didClose","params":{"textDocument":{"uri":"file:///Card.viu"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new SnapshotLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        List<JsonDocument> messages = await ReadAllMessagesAsync(output);
        JsonElement[] publications = messages
            .Where(message =>
                message.RootElement.TryGetProperty("method", out JsonElement method) &&
                method.GetString() == LanguageServerSemanticClassificationPublication.MethodName)
            .Select(message => message.RootElement.GetProperty("params"))
            .ToArray();
        publications.Length.ShouldBe(2);
        AssertOpenPublication(publications[0], 1, "checksum-1");
        publications[1].GetProperty("uri").GetString().ShouldBe("file:///Card.viu");
        publications[1].GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);
        publications[1].GetProperty("textChecksum").ValueKind.ShouldBe(JsonValueKind.Null);
        publications[1].GetProperty("isClosed").GetBoolean().ShouldBeTrue();
        publications[1].GetProperty("classifications").GetArrayLength().ShouldBe(0);

        foreach (JsonDocument message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_OrdinaryClient_DoesNotReceiveCustomClassificationNotification()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new SnapshotLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        List<JsonDocument> messages = await ReadAllMessagesAsync(output);
        messages.Any(message =>
                message.RootElement.TryGetProperty("method", out JsonElement method) &&
                method.GetString() == LanguageServerSemanticClassificationPublication.MethodName)
            .ShouldBeFalse();

        foreach (JsonDocument message in messages)
        {
            message.Dispose();
        }
    }

    private static void AssertOpenPublication(
        JsonElement publication,
        int expectedVersion,
        string expectedChecksum)
    {
        publication.GetProperty("uri").GetString().ShouldBe("file:///Card.viu");
        publication.GetProperty("version").GetInt32().ShouldBe(expectedVersion);
        publication.GetProperty("textChecksum").GetString().ShouldBe(expectedChecksum);
        publication.GetProperty("isClosed").GetBoolean().ShouldBeFalse();
        publication
            .GetProperty("classifications")
            .EnumerateArray()
            .Select(classification => classification
                .GetProperty("classificationTypeName")
                .GetString())
            .ShouldBe(
            [
                LanguageClassificationTypeNames.PropertyName,
                LanguageClassificationTypeNames.FieldName,
                LanguageClassificationTypeNames.LocalName,
            ]);
        JsonElement range = publication
            .GetProperty("classifications")[0]
            .GetProperty("range");
        range.GetProperty("start").GetProperty("line").GetInt32().ShouldBe(1);
        range.GetProperty("start").GetProperty("character").GetInt32().ShouldBe(4);
        range.GetProperty("end").GetProperty("character").GetInt32().ShouldBe(9);
    }

    private static async Task<List<JsonDocument>> ReadAllMessagesAsync(Stream stream)
    {
        var reader = new LanguageServerProtocolMessageReader(stream);
        var messages = new List<JsonDocument>();
        while (true)
        {
            JsonDocument? message = await reader.ReadAsync();
            if (message is null)
            {
                return messages;
            }

            messages.Add(message);
        }
    }

    private static string Frame(string payload)
        => $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";

    private sealed class SnapshotLanguageService : ILanguageService
    {
        private int? version;

        public void OpenDocument(string documentUri, string text, int? version) =>
            this.version = version;

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes)
        {
            this.version = version;
            return true;
        }

        public bool CloseDocument(string documentUri)
        {
            this.version = null;
            return true;
        }

        public LanguageClassificationSnapshot? GetClassificationSnapshot(
            string documentUri,
            CancellationToken cancellationToken = default)
            => CreateClassificationSnapshot(version);

        public LanguageDocumentPublication GetDocumentPublication(
            string documentUri,
            bool includeSemanticClassifications,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var capturedVersion = version;
            return new LanguageDocumentPublication(
                includeSemanticClassifications
                    ? CreateClassificationSnapshot(capturedVersion)
                    : null,
                []);
        }

        private static LanguageClassificationSnapshot CreateClassificationSnapshot(int? version)
            => new(
                version,
                $"checksum-{version}",
                [
                    new LanguageClassification(
                        new LanguageRange(
                            new LanguagePosition(1, 4),
                            new LanguagePosition(1, 9)),
                        LanguageClassificationTypeNames.PropertyName),
                    new LanguageClassification(
                        new LanguageRange(
                            new LanguagePosition(1, 10),
                            new LanguagePosition(1, 15)),
                        LanguageClassificationTypeNames.FieldName),
                    new LanguageClassification(
                        new LanguageRange(
                            new LanguagePosition(1, 16),
                            new LanguagePosition(1, 21)),
                        LanguageClassificationTypeNames.LocalName),
                ]);

        public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
            string documentUri,
            CancellationToken cancellationToken = default)
            => [];

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            => [];

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
            => [];

        public IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(
            string documentUri,
            CancellationToken cancellationToken = default)
            => [];

        public IReadOnlyList<LanguageCodeAction> GetCodeActions(
            string documentUri,
            LanguageRange range,
            CancellationToken cancellationToken = default)
            => [];
    }
}
