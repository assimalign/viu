using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// Pins textDocument/foldingRange serialization: one line-only fold per multi-line block, ending
/// one line above the closing delimiter.
/// </summary>
public class LanguageServerFoldingRangeTests
{
    [Fact]
    public async Task RunAsync_FoldingRangeRequest_ReturnsBlockContentRanges()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"<template>\n    <div />\n</template>\n@script {\n    public int Count;\n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"folding","method":"textDocument/foldingRange","params":{"textDocument":{"uri":"file:///Card.viu"}}}
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
        var ranges = messages[1].RootElement.GetProperty("result");
        ranges.GetArrayLength().ShouldBe(2);
        ranges[0].GetProperty("startLine").GetInt32().ShouldBe(0);
        ranges[0].GetProperty("endLine").GetInt32().ShouldBe(1);
        ranges[1].GetProperty("startLine").GetInt32().ShouldBe(3);
        ranges[1].GetProperty("endLine").GetInt32().ShouldBe(4);

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
}
