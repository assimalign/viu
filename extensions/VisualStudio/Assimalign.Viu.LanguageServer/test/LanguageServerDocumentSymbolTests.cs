using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Pins textDocument/documentSymbol serialization: hierarchical <c>DocumentSymbol</c> results for
/// clients that advertise <c>hierarchicalDocumentSymbolSupport</c>, and the flat
/// <c>SymbolInformation</c> shape for clients that do not.
/// </summary>
public class LanguageServerDocumentSymbolTests
{
    private const string DocumentUri = "file:///Card.viu";

    [Fact]
    public async Task RunAsync_DocumentSymbolRequest_ReturnsHierarchicalBlocks()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"documentSymbol":{"hierarchicalDocumentSymbolSupport":true}}}}}
                """) +
            Frame(OpenMessage()) +
            Frame(DocumentSymbolMessage()) +
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
        var symbols = messages[2].RootElement.GetProperty("result");
        symbols.GetArrayLength().ShouldBe(2);

        var template = symbols[0];
        template.GetProperty("name").GetString().ShouldBe("<template>");
        template.GetProperty("kind").GetInt32().ShouldBe(2);
        template.GetProperty("children").GetArrayLength().ShouldBe(0);
        var range = template.GetProperty("range");
        range.GetProperty("start").GetProperty("line").GetInt32().ShouldBe(0);
        var selectionRange = template.GetProperty("selectionRange");
        selectionRange.GetProperty("start").GetProperty("character").GetInt32().ShouldBe(1);

        var script = symbols[1];
        script.GetProperty("name").GetString().ShouldBe("@script");
        script.GetProperty("kind").GetInt32().ShouldBe(5);
        script.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32()
            .ShouldBe(3);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_DocumentSymbolRequest_FlattensWithoutHierarchySupport()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(OpenMessage()) +
            Frame(DocumentSymbolMessage()) +
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
        var symbols = messages[1].RootElement.GetProperty("result");
        symbols.GetArrayLength().ShouldBe(2);

        var template = symbols[0];
        template.GetProperty("name").GetString().ShouldBe("<template>");
        template.GetProperty("location").GetProperty("uri").GetString().ShouldBe(DocumentUri);
        template
            .GetProperty("location")
            .GetProperty("range")
            .GetProperty("start")
            .GetProperty("line")
            .GetInt32()
            .ShouldBe(0);
        template.TryGetProperty("selectionRange", out _).ShouldBeFalse();
        template.TryGetProperty("children", out _).ShouldBeFalse();

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    private static string OpenMessage()
        => """
           {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"<template>\n    <div />\n</template>\n@script {\n    public int Count;\n}\n"}}}
           """;

    private static string DocumentSymbolMessage()
        => """
           {"jsonrpc":"2.0","id":"symbols","method":"textDocument/documentSymbol","params":{"textDocument":{"uri":"file:///Card.viu"}}}
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

    private static string Frame(string payload)
        => $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";
}
