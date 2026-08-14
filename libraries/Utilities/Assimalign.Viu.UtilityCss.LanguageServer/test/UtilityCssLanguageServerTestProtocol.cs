using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Assimalign.Viu.UtilityCss.LanguageServer;

namespace Assimalign.Viu.UtilityCss.LanguageServer.Tests;

internal static class UtilityCssLanguageServerTestProtocol
{
    internal static async Task<(int ExitCode, List<JsonDocument> Messages)> RunAsync(
        params string[] payloads)
    {
        var framedMessages = new StringBuilder();
        foreach (var payload in payloads)
        {
            framedMessages.Append(Frame(payload));
        }

        await using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(framedMessages.ToString()));
        await using var output = new MemoryStream();
        var host = new LanguageServerHost();

        var exitCode = await host.RunAsync(input, output);

        output.Position = 0;
        return (exitCode, await ReadAllMessagesAsync(output));
    }

    internal static string InitializeRequest(string identifier) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["capabilities"] = new JsonObject
                {
                    ["textDocument"] = new JsonObject
                    {
                        ["completion"] = new JsonObject
                        {
                            ["completionItem"] = new JsonObject
                            {
                                ["documentationFormat"] = new JsonArray(
                                    JsonValue.Create("markdown")),
                            },
                        },
                        ["hover"] = new JsonObject
                        {
                            ["contentFormat"] = new JsonArray(
                                JsonValue.Create("markdown")),
                        },
                    },
                },
            },
        }.ToJsonString();

    internal static string InitializedNotification() =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "initialized",
            ["params"] = new JsonObject(),
        }.ToJsonString();

    internal static string DidOpenNotification(
        string documentUri,
        string languageIdentifier,
        string text) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "textDocument/didOpen",
            ["params"] = new JsonObject
            {
                ["textDocument"] = new JsonObject
                {
                    ["uri"] = documentUri,
                    ["languageId"] = languageIdentifier,
                    ["version"] = 1,
                    ["text"] = text,
                },
            },
        }.ToJsonString();

    internal static string CompletionRequest(
        string identifier,
        string documentUri,
        string text,
        int position) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "textDocument/completion",
            ["params"] = new JsonObject
            {
                ["textDocument"] = TextDocument(documentUri),
                ["position"] = Position(text, position),
            },
        }.ToJsonString();

    internal static string HoverRequest(
        string identifier,
        string documentUri,
        string text,
        int position) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "textDocument/hover",
            ["params"] = new JsonObject
            {
                ["textDocument"] = TextDocument(documentUri),
                ["position"] = Position(text, position),
            },
        }.ToJsonString();

    internal static string CompletionResolveRequest(
        string identifier,
        string documentUri,
        string label) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "completionItem/resolve",
            ["params"] = new JsonObject
            {
                ["label"] = label,
                ["data"] = new JsonObject
                {
                    ["documentUri"] = documentUri,
                    ["label"] = label,
                },
            },
        }.ToJsonString();

    internal static string DocumentColorRequest(
        string identifier,
        string documentUri) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "textDocument/documentColor",
            ["params"] = new JsonObject
            {
                ["textDocument"] = TextDocument(documentUri),
            },
        }.ToJsonString();

    internal static string ColorPresentationRequest(
        string identifier,
        string documentUri,
        string text,
        int rangeStart,
        int rangeEnd,
        double red,
        double green,
        double blue,
        double alpha) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "textDocument/colorPresentation",
            ["params"] = new JsonObject
            {
                ["textDocument"] = TextDocument(documentUri),
                ["color"] = new JsonObject
                {
                    ["red"] = red,
                    ["green"] = green,
                    ["blue"] = blue,
                    ["alpha"] = alpha,
                },
                ["range"] = new JsonObject
                {
                    ["start"] = Position(text, rangeStart),
                    ["end"] = Position(text, rangeEnd),
                },
            },
        }.ToJsonString();

    internal static string ShutdownRequest(string identifier) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = identifier,
            ["method"] = "shutdown",
            ["params"] = null,
        }.ToJsonString();

    internal static string ExitNotification() =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "exit",
        }.ToJsonString();

    internal static JsonElement FindResponse(
        IReadOnlyList<JsonDocument> messages,
        string identifier)
    {
        foreach (var message in messages)
        {
            var root = message.RootElement;
            if (root.TryGetProperty("id", out var responseIdentifier) &&
                responseIdentifier.ValueKind == JsonValueKind.String &&
                string.Equals(
                    responseIdentifier.GetString(),
                    identifier,
                    StringComparison.Ordinal))
            {
                return root;
            }
        }

        throw new InvalidOperationException(
            $"No response with identifier '{identifier}' was written by the server.");
    }

    internal static JsonElement FindCompletionItem(
        JsonElement items,
        string label)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (string.Equals(
                    item.GetProperty("label").GetString(),
                    label,
                    StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InvalidOperationException(
            $"No completion item with label '{label}' was returned.");
    }

    internal static void DisposeMessages(IEnumerable<JsonDocument> messages)
    {
        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    internal static (int Line, int Character) GetPosition(
        string text,
        int position)
    {
        var line = 0;
        var lineStart = 0;
        for (var index = 0; index < position; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, position - lineStart);
    }

    private static JsonObject Position(string text, int position)
    {
        var mapped = GetPosition(text, position);
        return new JsonObject
        {
            ["line"] = mapped.Line,
            ["character"] = mapped.Character,
        };
    }

    private static JsonObject TextDocument(string documentUri) =>
        new()
        {
            ["uri"] = documentUri,
        };

    private static async Task<List<JsonDocument>> ReadAllMessagesAsync(Stream stream)
    {
        var reader = new LanguageServerProtocolMessageReader(stream);
        var messages = new List<JsonDocument>();
        while (await reader.ReadAsync() is { } message)
        {
            messages.Add(message);
        }

        return messages;
    }

    private static string Frame(string payload) =>
        $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";
}
