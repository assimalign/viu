using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins the completionItem/resolve round-trip for deferred semantic documentation.
/// </summary>
public class LanguageServerResolveTests
{
    private const string DocumentUri = "file:///Card.viu";

    [Fact]
    public async Task RunAsync_CompletionResult_CarriesResolveDataAndOmitsDeferredDocumentation()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(CompletionMessage()) +
            Frame("""{"jsonrpc":"2.0","method":"exit"}"""));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new DeferredDocumentationLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var count = FindItem(
            messages[1].RootElement.GetProperty("result").GetProperty("items"),
            "Count");
        count.HasValue.ShouldBeTrue();
        count!.Value.TryGetProperty("documentation", out _).ShouldBeFalse();
        var data = count.Value.GetProperty("data");
        data.GetProperty("documentUri").GetString().ShouldBe(DocumentUri);
        data.GetProperty("label").GetString().ShouldBe("Count");

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_CompletionItemResolve_ReturnsSemanticDocumentation()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"resolve","method":"completionItem/resolve","params":{"label":"Count","data":{"documentUri":"file:///Card.viu","label":"Count"}}}
                """) +
            Frame("""{"jsonrpc":"2.0","method":"exit"}"""));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new DeferredDocumentationLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var resolved = messages[1].RootElement.GetProperty("result");
        resolved.GetProperty("label").GetString().ShouldBe("Count");
        var documentation = resolved.GetProperty("documentation");
        // No initialize was sent, so the negotiated documentation format is plaintext.
        documentation.GetProperty("kind").GetString().ShouldBe("plaintext");
        documentation.GetProperty("value").GetString()!
            .ShouldContain("int Count { get; set; }");

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_CompletionItemResolve_UnknownDocument_EchoesItemUnchanged()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":"resolve","method":"completionItem/resolve","params":{"label":"Count","sortText":"00:Count","data":{"documentUri":"file:///Closed.viu","label":"Count"}}}
                """) +
            Frame("""{"jsonrpc":"2.0","method":"exit"}"""));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new DeferredDocumentationLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var response = messages[0].RootElement;
        response.TryGetProperty("error", out _).ShouldBeFalse();
        var resolved = response.GetProperty("result");
        resolved.GetProperty("label").GetString().ShouldBe("Count");
        resolved.GetProperty("sortText").GetString().ShouldBe("00:Count");
        resolved.TryGetProperty("documentation", out _).ShouldBeFalse();

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    private static string OpenMessage()
        => """
           {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\n    \n}\n"}}}
           """;

    private static string CompletionMessage()
        => """
           {"jsonrpc":"2.0","id":"completion","method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Card.viu"},"position":{"line":1,"character":4}}}
           """;

    private static JsonElement? FindItem(JsonElement items, string label)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.GetProperty("label").GetString() == label)
            {
                return item;
            }
        }

        return null;
    }

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

    private sealed class DeferredDocumentationLanguageService : ILanguageService
    {
        public void OpenDocument(string documentUri, string text, int? version)
        {
        }

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes) => true;

        public bool CloseDocument(string documentUri) => true;

        public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
            string documentUri,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageDiagnostic>();

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            =>
            [
                new LanguageCompletionItem(
                    "Count",
                    LanguageCompletionItemKind.Property,
                    "int Count { get; set; }",
                    Documentation: string.Empty,
                    "Count",
                    IsSnippet: false,
                    SortText: "00:Count"),
            ];

        public LanguageHover? GetHover(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            => null;

        public string? ResolveCompletionDocumentation(
            string documentUri,
            string completionLabel,
            CancellationToken cancellationToken = default)
            => completionLabel == "Count"
                ? "```csharp\nint Count { get; set; }\n```\nCounts clicks."
                : null;

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
