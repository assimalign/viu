using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Pins the completionItem/resolve round-trip: completion items carry a
/// <c>data</c> payload (document URI + label) instead of inline documentation, resolve computes
/// the documentation on demand, and a resolve racing a closed document echoes the item back
/// without a protocol error.
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
        var gap = FindItem(
            messages[1].RootElement.GetProperty("result").GetProperty("items"),
            "gap-4");
        gap.HasValue.ShouldBeTrue();
        gap!.Value.TryGetProperty("documentation", out _).ShouldBeFalse();
        var data = gap.Value.GetProperty("data");
        data.GetProperty("documentUri").GetString().ShouldBe(DocumentUri);
        data.GetProperty("label").GetString().ShouldBe("gap-4");

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_CompletionItemResolve_ReturnsUtilityDocumentation()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"resolve","method":"completionItem/resolve","params":{"label":"gap-4","data":{"documentUri":"file:///Card.viu","label":"gap-4"}}}
                """) +
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
        var resolved = messages[1].RootElement.GetProperty("result");
        resolved.GetProperty("label").GetString().ShouldBe("gap-4");
        var documentation = resolved.GetProperty("documentation");
        // No initialize was sent, so the negotiated documentation format is plaintext.
        documentation.GetProperty("kind").GetString().ShouldBe("plaintext");
        documentation.GetProperty("value").GetString()!
            .ShouldContain("gap: calc(var(--spacing) * 4);");

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
                {"jsonrpc":"2.0","id":"resolve","method":"completionItem/resolve","params":{"label":"gap-4","sortText":"00001:gap-4","data":{"documentUri":"file:///Closed.viu","label":"gap-4"}}}
                """) +
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
        var response = messages[0].RootElement;
        response.TryGetProperty("error", out _).ShouldBeFalse();
        var resolved = response.GetProperty("result");
        resolved.GetProperty("label").GetString().ShouldBe("gap-4");
        resolved.GetProperty("sortText").GetString().ShouldBe("00001:gap-4");
        resolved.TryGetProperty("documentation", out _).ShouldBeFalse();

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    private static string OpenMessage()
        => """
           {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"<template>\n    <div class=\"gap-\"></div>\n</template>\n"}}}
           """;

    private static string CompletionMessage()
        => """
           {"jsonrpc":"2.0","id":"completion","method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Card.viu"},"position":{"line":1,"character":20}}}
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
}
