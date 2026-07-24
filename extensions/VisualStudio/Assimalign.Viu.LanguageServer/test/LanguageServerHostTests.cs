using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer;

public class LanguageServerHostTests
{
    [Fact]
    public async Task RunAsync_EditorSession_InitializesPublishesDiagnosticsAndCompletes()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Counter.viu","languageId":"viu","version":1,"text":"@script\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":2,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":0,"character":7}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":3,"method":"shutdown","params":null}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost();

        var exitCode = await host.RunAsync(input, output);

        exitCode.ShouldBe(0);
        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        messages.Count.ShouldBe(4);

        var initialize = messages[0].RootElement;
        initialize.GetProperty("id").GetInt32().ShouldBe(1);
        initialize
            .GetProperty("result")
            .GetProperty("capabilities")
            .GetProperty("completionProvider")
            .GetProperty("triggerCharacters")
            .GetArrayLength()
            .ShouldBeGreaterThan(0);

        var diagnostics = messages[1].RootElement;
        diagnostics.GetProperty("method").GetString().ShouldBe("textDocument/publishDiagnostics");
        diagnostics
            .GetProperty("params")
            .GetProperty("diagnostics")[0]
            .GetProperty("code")
            .GetString()
            .ShouldBe("VIU1003");

        var completion = messages[2].RootElement;
        completion.GetProperty("id").GetInt32().ShouldBe(2);
        completion
            .GetProperty("result")
            .GetProperty("items")
            .GetArrayLength()
            .ShouldBeGreaterThan(0);

        messages[3].RootElement.GetProperty("id").GetInt32().ShouldBe(3);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_HoverRequest_ReturnsMarkdownHover()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Counter.viu","languageId":"viu","version":1,"text":"@script {\n    Reactive.Reference(0);\n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":"hover","method":"textDocument/hover","params":{"textDocument":{"uri":"file:///Counter.viu"},"position":{"line":1,"character":18}}}
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
        messages.Count.ShouldBe(2);
        messages[1].RootElement
            .GetProperty("result")
            .GetProperty("contents")
            .GetProperty("kind")
            .GetString()
            .ShouldBe("markdown");

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
