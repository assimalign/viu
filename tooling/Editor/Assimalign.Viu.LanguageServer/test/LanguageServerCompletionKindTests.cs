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
/// Pins the completion-kind boundary. The host performs no kind translation of its own: the service
/// already speaks the Language Server Protocol's <c>CompletionItemKind</c> numbering, and the host
/// serializes that value verbatim. A namespace therefore reaches the editor as <c>Module</c> (9),
/// the value editors render with the namespace glyph — Visual Studio draws it as <c>{}</c>. A
/// generic color completion reaches it as <c>Color</c> (16) with its value in item data. This pins
/// the dormant transport independently of any current completion producer. Stock clients can use
/// the standard color-category presentation without interpreting the opaque data.
/// Snippet insert text crosses unchanged to a client that advertised snippet support: an escaped
/// dollar sign keeps the authored <c>$event</c> identifier literal while the numbered placeholder
/// remains active. A client that advertised none receives the placeholder-free rendering instead,
/// because it would otherwise insert <c>$1</c> into the author's buffer as text
/// ([V01.01.12.07.12], #333; [V01.01.12.07.13], #334).
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#completionItemKind">
/// Language Server Protocol 3.17, <c>CompletionItemKind</c></see>.
/// </summary>
public class LanguageServerCompletionKindTests
{
    [Fact]
    public async Task RunAsync_Completions_SerializeKindsDormantColorPayloadAndSnippetEscape()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\nusing \n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Card.viu"},"position":{"line":1,"character":6}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new StubLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completion = messages.Find(
            message =>
                message.RootElement.TryGetProperty("id", out var identifier) &&
                identifier.ValueKind == JsonValueKind.Number);

        completion.ShouldNotBeNull();
        var items = completion.RootElement.GetProperty("result").GetProperty("items");
        FindItem(items, "System")!.Value.GetProperty("kind").GetInt32().ShouldBe(9);
        FindItem(items, "String")!.Value.GetProperty("kind").GetInt32().ShouldBe(7);
        var color = FindItem(items, "BrandColor")!.Value;
        color.GetProperty("kind").GetInt32().ShouldBe(16);
        color.GetProperty("data").GetProperty("colorValue").GetString()
            .ShouldBe("#123456");
        // No initialize request ran, so the client advertised nothing and the snippet downgrades:
        // the escape resolves to the literal dollar sign the author wants and the tabstop is dropped.
        var eventLambda = FindItem(items, "$event lambda")!.Value;
        eventLambda.GetProperty("insertText").GetString().ShouldBe("$event => ");
        eventLambda.GetProperty("insertTextFormat").GetInt32().ShouldBe(1);
        var asynchronousEventLambda = FindItem(items, "async $event lambda")!.Value;
        asynchronousEventLambda.GetProperty("insertText").GetString()
            .ShouldBe("async $event => ");
        asynchronousEventLambda.GetProperty("insertTextFormat").GetInt32().ShouldBe(1);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_CompletionsForSnippetCapableClient_KeepTheSnippetFormat()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"completion":{"completionItem":{"snippetSupport":true}}}}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\nusing \n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":2,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Card.viu"},"position":{"line":1,"character":6}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new StubLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completion = messages.Find(
            message =>
                message.RootElement.TryGetProperty("id", out var identifier) &&
                identifier.ValueKind == JsonValueKind.Number &&
                identifier.GetInt32() == 2);

        completion.ShouldNotBeNull();
        var items = completion!.RootElement.GetProperty("result").GetProperty("items");
        var eventLambda = FindItem(items, "$event lambda")!.Value;
        eventLambda.GetProperty("insertText").GetString().ShouldBe("\\$event => $1");
        eventLambda.GetProperty("insertTextFormat").GetInt32().ShouldBe(2);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    /// <summary>
    /// The narrowest service the host can drive: a fixed namespace-and-type completion answer, so
    /// the assertion is about the protocol boundary rather than about the semantic engine's ability
    /// to bind a namespace from an on-disk project.
    /// </summary>
    private sealed class StubLanguageService : ILanguageService
    {
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

        public IReadOnlyList<LanguageClassification> GetClassifications(
            string documentUri,
            CancellationToken cancellationToken = default)
            => Array.Empty<LanguageClassification>();

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
            =>
            [
                new LanguageCompletionItem(
                    "System",
                    LanguageCompletionItemKind.Module,
                    "namespace System",
                    Documentation: string.Empty,
                    "System",
                    IsSnippet: false,
                    SortText: "00:System"),
                new LanguageCompletionItem(
                    "String",
                    LanguageCompletionItemKind.Class,
                    "class String",
                    Documentation: string.Empty,
                    "String",
                    IsSnippet: false,
                    SortText: "01:String"),
                new LanguageCompletionItem(
                    "BrandColor",
                    LanguageCompletionItemKind.Color,
                    "CSS color completion",
                    Documentation: string.Empty,
                    "BrandColor",
                    IsSnippet: false,
                    SortText: "02:BrandColor",
                    ColorValue: "#123456"),
                new LanguageCompletionItem(
                    "$event lambda",
                    LanguageCompletionItemKind.Snippet,
                    "Template event lambda",
                    Documentation: string.Empty,
                    "\\$event => $1",
                    IsSnippet: true,
                    SortText: "03:event-lambda"),
                new LanguageCompletionItem(
                    "async $event lambda",
                    LanguageCompletionItemKind.Snippet,
                    "Asynchronous template event lambda",
                    Documentation: string.Empty,
                    "async \\$event => $1",
                    IsSnippet: true,
                    SortText: "04:async-event-lambda"),
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
}
