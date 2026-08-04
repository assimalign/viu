using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// Pins the tier-two capability declarations and the client-capability honesty rules: the server
/// advertises resolve/symbol/folding/code-action support, and emits hover content in the format the
/// client actually advertised — Visual Studio advertises plaintext and renders Markdown literally
/// (https://github.com/microsoft/VSExtensibility/issues/271), so plaintext is also the
/// no-initialize default.
/// </summary>
public class LanguageServerCapabilityTests
{
    [Fact]
    public async Task RunAsync_Initialize_DeclaresTierTwoCapabilities()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}
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
        var capabilities = messages[0].RootElement
            .GetProperty("result")
            .GetProperty("capabilities");
        capabilities
            .GetProperty("completionProvider")
            .GetProperty("resolveProvider")
            .GetBoolean()
            .ShouldBeTrue();
        capabilities.GetProperty("documentSymbolProvider").GetBoolean().ShouldBeTrue();
        capabilities.GetProperty("foldingRangeProvider").GetBoolean().ShouldBeTrue();
        capabilities
            .GetProperty("codeActionProvider")
            .GetProperty("codeActionKinds")
            .EnumerateArray()
            .Select(kind => kind.GetString())
            .ShouldBe(new[] { "quickfix" });

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_HoverWithMarkdownCapability_ReturnsMarkdownContents()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"hover":{"contentFormat":["markdown","plaintext"]}}}}}
                """) +
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
        messages.Count.ShouldBe(3);
        var contents = messages[2].RootElement
            .GetProperty("result")
            .GetProperty("contents");
        contents.GetProperty("kind").GetString().ShouldBe("markdown");
        contents.GetProperty("value").GetString()!.ShouldContain("**");

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_HoverWithPlaintextClient_StripsMarkdownMarkers(
        bool sendPlaintextInitialize)
    {
        var initializeFrame = sendPlaintextInitialize
            ? Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"hover":{"contentFormat":["plaintext"]}}}}}
                """)
            : string.Empty;
        var inputBytes = Encoding.UTF8.GetBytes(
            initializeFrame +
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
        var contents = messages[^1].RootElement
            .GetProperty("result")
            .GetProperty("contents");
        contents.GetProperty("kind").GetString().ShouldBe("plaintext");
        var value = contents.GetProperty("value").GetString();
        value.ShouldNotBeNull();
        value!.ShouldContain("Reference<T>");
        value.ShouldNotContain("**");
        value.ShouldNotContain("`");

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
