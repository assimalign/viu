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
/// Pins the
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_semanticTokens">
/// Language Server Protocol 3.17 full semantic-token response</see> for projected C#
/// classifications. The integer stream uses the standard relative encoding and the legend
/// advertised at initialize.
/// </summary>
public class LanguageServerSemanticTokenTests
{
    [Fact]
    public async Task RunAsync_FullSemanticTokenRequest_EncodesSortedAuthoredClassifications()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\n    int Value;\n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"tokens","method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///Card.viu"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new ClassificationLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var data = messages
            .Single(message => message.RootElement.TryGetProperty("id", out _))
            .RootElement
            .GetProperty("result")
            .GetProperty("data")
            .EnumerateArray()
            .Select(value => value.GetInt32());
        data.ShouldBe(
        [
            1, 10, 10, 0, 0,
            1, 4, 5, 9, 0,
            0, 11, 4, 7, 0,
        ]);

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

    private sealed class ClassificationLanguageService : ILanguageService
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
            => [];

        public IReadOnlyList<LanguageClassification> GetClassifications(
            string documentUri,
            CancellationToken cancellationToken = default)
            =>
            [
                new LanguageClassification(
                    new LanguageRange(new LanguagePosition(2, 15), new LanguagePosition(2, 19)),
                    LanguageClassificationTypeNames.ParameterName),
                new LanguageClassification(
                    new LanguageRange(new LanguagePosition(1, 10), new LanguagePosition(1, 20)),
                    LanguageClassificationTypeNames.NamespaceName),
                new LanguageClassification(
                    new LanguageRange(new LanguagePosition(2, 4), new LanguagePosition(2, 9)),
                    LanguageClassificationTypeNames.PropertyName),
            ];

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
