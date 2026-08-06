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
/// Pins the request cancellation and dispatch model ([V01.01.12.23], #259): <c>$/cancelRequest</c>
/// answers its in-flight target with the protocol's RequestCancelled code (-32800), cancellation of
/// an unknown identifier is a silent no-op, a notification read before a request is observed by
/// that request's computation, and two in-flight requests answer concurrently.
/// </summary>
public class LanguageServerCancellationTests
{
    [Fact]
    public async Task RunAsync_CancelRequestForInFlightCompletion_RepliesRequestCancelled()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(
                """
                {"jsonrpc":"2.0","id":7,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":0}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"$/cancelRequest","params":{"id":7}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":8,"method":"shutdown","params":null}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new BlockingCompletionLanguageService());

        var exitCode = await host.RunAsync(input, output);

        exitCode.ShouldBe(0);
        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completionResponses = messages
            .Where(
                message =>
                    message.RootElement.TryGetProperty("id", out var identifier) &&
                    identifier.ValueKind == JsonValueKind.Number &&
                    identifier.GetInt32() == 7)
            .ToArray();
        completionResponses.Length.ShouldBe(1);
        var response = completionResponses[0].RootElement;
        response.TryGetProperty("result", out _).ShouldBeFalse();
        response.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32800);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_CancelRequestForUnknownIdentifier_ProducesNoOutput()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"$/cancelRequest","params":{"id":99}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost();

        await host.RunAsync(input, output);

        // Cancellation of an unknown or already finished request is a silent no-op: no reply and
        // no protocol error may be produced.
        output.Length.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DidChangeBeforeCompletionRequest_ObservesAppliedChange()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///Counter.viu","version":2},"contentChanges":[{"text":"@script { }\n"}]}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"completion","method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":0}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new ChangeCountingLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completion = FindResponse(messages, "completion");
        var items = completion.GetProperty("result").GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        // End-to-end proof of the ordering contract: the didChange read before the request was
        // applied before the dispatched request computed.
        items[0].GetProperty("label").GetString().ShouldBe("observed-change-count:1");

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_ConcurrentCompletionAndHoverRequests_BothAnswer()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"a","method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":0}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"b","method":"textDocument/hover","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":0}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        using var bothRequestsStarted = new CountdownEvent(2);
        var host = new LanguageServerHost(new RendezvousLanguageService(bothRequestsStarted));

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completion = FindResponse(messages, "a");
        var hover = FindResponse(messages, "b");
        // A regression to serial dispatch surfaces as -32603 timeout replies from the rendezvous
        // fake instead of a hung test, so the absence of errors pins genuine concurrency.
        completion.TryGetProperty("error", out _).ShouldBeFalse();
        hover.TryGetProperty("error", out _).ShouldBeFalse();
        completion.TryGetProperty("result", out _).ShouldBeTrue();
        hover.TryGetProperty("result", out _).ShouldBeTrue();

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    private static string OpenMessage()
        => """
           {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Counter.viu","languageId":"viu","version":1,"text":"@script\n"}}}
           """;

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

    private static JsonElement FindResponse(List<JsonDocument> messages, string identifier)
        => messages.Single(
                message =>
                    message.RootElement.TryGetProperty("id", out var responseIdentifier) &&
                    responseIdentifier.ValueKind == JsonValueKind.String &&
                    responseIdentifier.GetString() == identifier)
            .RootElement;

    private static string Frame(string payload)
        => $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";

    /// <summary>
    /// A service whose completion blocks until its token cancels. The bounded wait turns a lost
    /// cancellation into a fast assertion failure (a successful empty reply) instead of a hang.
    /// </summary>
    private sealed class BlockingCompletionLanguageService : IViuLanguageService
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

        public IReadOnlyList<LanguageCompletionItem> GetCompletions(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
            return Array.Empty<LanguageCompletionItem>();
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

    /// <summary>
    /// A service whose completion reports how many document changes it observed at call time,
    /// proving a dispatched request sees every notification read before it.
    /// </summary>
    private sealed class ChangeCountingLanguageService : IViuLanguageService
    {
        private int changeCount;

        public void OpenDocument(string documentUri, string text, int? version)
        {
        }

        public bool ChangeDocument(
            string documentUri,
            int? version,
            IReadOnlyList<LanguageDocumentChange> changes)
        {
            changeCount++;
            return true;
        }

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
                    $"observed-change-count:{changeCount}",
                    LanguageCompletionItemKind.Property,
                    "Change observation probe",
                    "Reports the change count observed at completion time.",
                    $"observed-change-count:{changeCount}",
                    IsSnippet: false,
                    SortText: "01"),
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

    /// <summary>
    /// A service whose completion and hover each signal a shared countdown and then wait for the
    /// other, so both requests can only answer when they are in flight at the same time. A
    /// regression to serial dispatch times the first request out after ten seconds and surfaces as
    /// a -32603 reply — a clear failure rather than a hung test.
    /// </summary>
    private sealed class RendezvousLanguageService : IViuLanguageService
    {
        private readonly CountdownEvent bothRequestsStarted;

        internal RendezvousLanguageService(CountdownEvent bothRequestsStarted)
            => this.bothRequestsStarted = bothRequestsStarted;

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
        {
            Rendezvous();
            return Array.Empty<LanguageCompletionItem>();
        }

        public LanguageHover? GetHover(
            string documentUri,
            LanguagePosition position,
            CancellationToken cancellationToken = default)
        {
            Rendezvous();
            return null;
        }

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

        private void Rendezvous()
        {
            bothRequestsStarted.Signal();
            if (!bothRequestsStarted.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The paired concurrent request never started.");
            }
        }
    }
}
