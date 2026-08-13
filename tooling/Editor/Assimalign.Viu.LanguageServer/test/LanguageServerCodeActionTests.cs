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
/// Pins textDocument/codeAction serialization: the VIU1206 quickfix ships as a code-action literal
/// with a workspace edit against the requesting document, and clients that never advertised
/// <c>codeActionLiteralSupport</c> receive an empty result because this server implements no
/// commands.
/// </summary>
public class LanguageServerCodeActionTests
{
    [Fact]
    public async Task RunAsync_CodeActionRequest_ReturnsQuickfixWorkspaceEdit()
    {
        // .vue support requires an owning Viu project, so the document lives in a temporary
        // directory whose csproj uses the Assimalign.Viu.Sdk.
        var directory = Path.Combine(
            Path.GetTempPath(),
            "viu-language-server-code-action-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Application.csproj"),
                "<Project Sdk=\"Assimalign.Viu.Sdk\" />");
            const string source =
                "<template><div /></template>\n" +
                "<script>export default {}</script>";
            var componentPath = Path.Combine(directory, "Card.vue");
            File.WriteAllText(componentPath, source);
            var documentUri = new Uri(componentPath).AbsoluteUri;

            var openMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    method = "textDocument/didOpen",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                            languageId = "vue",
                            version = 1,
                            text = source,
                        },
                    },
                });
            var codeActionMessage = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "actions",
                    method = "textDocument/codeAction",
                    @params = new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                        },
                        range = new
                        {
                            start = new { line = 1, character = 0 },
                            end = new { line = 1, character = 10 },
                        },
                        context = new { diagnostics = Array.Empty<object>() },
                    },
                });
            var inputBytes = Encoding.UTF8.GetBytes(
                Frame(
                    """
                    {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"codeAction":{"codeActionLiteralSupport":{"codeActionKind":{"valueSet":["quickfix"]}}}}}}}
                    """) +
                Frame(openMessage) +
                Frame(codeActionMessage) +
                Frame(
                    """
                    {"jsonrpc":"2.0","method":"exit"}
                    """));

            await using var input = new MemoryStream(inputBytes);
            await using var output = new MemoryStream();
            var host = new LanguageServerHost();

            await host.RunAsync(input, output);

            output.Position = 0;
            var messages = await ReadAllMessagesAsync(output);
            // The unrestored temporary project also produces a project-context status notification
            // ([V01.01.12.23], #259), so the response is located by identifier rather than index.
            var actions = FindResponse(messages, "actions").GetProperty("result");
            actions.GetArrayLength().ShouldBe(1);
            var action = actions[0];
            action.GetProperty("title").GetString().ShouldBe("Use lang=\"csharp\"");
            action.GetProperty("kind").GetString().ShouldBe("quickfix");
            action.GetProperty("isPreferred").GetBoolean().ShouldBeTrue();
            action.GetProperty("diagnostics")[0].GetProperty("code").GetString()
                .ShouldBe("VIU1206");
            var edit = action
                .GetProperty("edit")
                .GetProperty("changes")
                .GetProperty(documentUri)[0];
            edit.GetProperty("newText").GetString().ShouldBe(" lang=\"csharp\"");
            var editRange = edit.GetProperty("range");
            editRange.GetProperty("start").GetProperty("line").GetInt32().ShouldBe(1);
            editRange.GetProperty("start").GetProperty("character").GetInt32()
                .ShouldBe("<script".Length);
            editRange.GetProperty("end").GetProperty("character").GetInt32()
                .ShouldBe("<script".Length);

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
    public async Task RunAsync_CodeActionWithoutLiteralSupport_ReturnsEmptyArray()
    {
        // The fake service always offers an action, so an empty result pins the capability gate
        // itself: without codeActionLiteralSupport only Command[] results are legal.
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"<template><div /></template>\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"actions","method":"textDocument/codeAction","params":{"textDocument":{"uri":"file:///Card.viu"},"range":{"start":{"line":0,"character":0},"end":{"line":0,"character":5}},"context":{"diagnostics":[]}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(new QuickfixLanguageService());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var response = messages[1].RootElement;
        response.TryGetProperty("error", out _).ShouldBeFalse();
        response.GetProperty("result").GetArrayLength().ShouldBe(0);

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

    private static JsonElement FindResponse(List<JsonDocument> messages, string identifier)
        => messages.Single(
                message =>
                    message.RootElement.TryGetProperty("id", out var responseIdentifier) &&
                    responseIdentifier.ValueKind == JsonValueKind.String &&
                    responseIdentifier.GetString() == identifier)
            .RootElement;

    private sealed class QuickfixLanguageService : ILanguageService
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
            =>
            [
                new LanguageCodeAction(
                    "Use lang=\"csharp\"",
                    "quickfix",
                    Array.Empty<LanguageDiagnostic>(),
                    [
                        new LanguageTextEdit(
                            new LanguageRange(
                                new LanguagePosition(0, 0),
                                new LanguagePosition(0, 0)),
                            " lang=\"csharp\""),
                    ],
                    IsPreferred: true),
            ];
    }
}
