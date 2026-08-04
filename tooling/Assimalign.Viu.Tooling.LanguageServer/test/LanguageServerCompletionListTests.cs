using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.Tooling.LanguageService;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// Pins the completion-list completeness flag. A truncated utility result reported as complete makes
/// the client filter its cached page instead of re-requesting, so the narrower candidate the author
/// is typing toward is never offered.
/// </summary>
public class LanguageServerCompletionListTests
{
    [Theory]
    [InlineData(LanguageCompletionLimits.MaximumItems, true)]
    [InlineData(3, false)]
    public async Task RunAsync_CompletionResult_ReportsIncompleteOnlyWhenTruncated(
        int itemCount,
        bool expectedIsIncomplete)
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"<template>\n    <div class=\"\"></div>\n</template>\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Card.viu"},"position":{"line":1,"character":16}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new FixedCountLanguageService(itemCount));

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completion = messages.Find(
            message =>
                message.RootElement.TryGetProperty("id", out var identifier) &&
                identifier.ValueKind == JsonValueKind.Number);

        completion.ShouldNotBeNull();
        var result = completion.RootElement.GetProperty("result");
        result.GetProperty("items").GetArrayLength().ShouldBe(itemCount);
        result.GetProperty("isIncomplete").GetBoolean().ShouldBe(expectedIsIncomplete);

        foreach (var message in messages)
        {
            message.Dispose();
        }
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

    private sealed class FixedCountLanguageService : IViuLanguageService
    {
        private readonly int itemCount;

        internal FixedCountLanguageService(int itemCount) => this.itemCount = itemCount;

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
        {
            var items = new LanguageCompletionItem[itemCount];
            for (var index = 0; index < itemCount; index++)
            {
                items[index] = new LanguageCompletionItem(
                    $"gap-{index}",
                    LanguageCompletionItemKind.Property,
                    "Viu utility class",
                    "documentation",
                    $"gap-{index}",
                    IsSnippet: false,
                    SortText: $"{index:D5}");
            }

            return items;
        }

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
