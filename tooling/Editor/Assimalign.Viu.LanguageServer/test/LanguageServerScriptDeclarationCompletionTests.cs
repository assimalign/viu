using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins declared-member completion end to end through the protocol harness with the real language
/// service ([V01.01.12.07.04], #261): a member declared in a <c>.viu</c> document's <c>@script</c>
/// block comes back as a completion item with its declared type detail and the field kind.
/// </summary>
public class LanguageServerScriptDeclarationCompletionTests
{
    [Fact]
    public async Task RunAsync_ScriptBlockDeclaringField_OffersDeclaredMemberWithTypeDetail()
    {
        var inputBytes = Encoding.UTF8.GetBytes(
            Frame(
                """
                {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///Card.viu","languageId":"viu","version":1,"text":"@script {\n    public Reference<int> Counter = Reactive.Reference(0);\n    \n}\n"}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","id":1,"method":"textDocument/completion","params":{"textDocument":{"uri":"file:///Card.viu"},"position":{"line":2,"character":4}}}
                """) +
            Frame(
                """
                {"jsonrpc":"2.0","method":"exit"}
                """));

        await using var input = new MemoryStream(inputBytes);
        await using var output = new MemoryStream();
        var host = new LanguageServerHost(LanguageServices.Create());

        await host.RunAsync(input, output);

        output.Position = 0;
        var messages = await ReadAllMessagesAsync(output);
        var completion = messages.Find(
            message =>
                message.RootElement.TryGetProperty("id", out var identifier) &&
                identifier.ValueKind == JsonValueKind.Number);

        completion.ShouldNotBeNull();
        var items = completion.RootElement.GetProperty("result").GetProperty("items");
        var declared = FindItem(items, "Counter");
        declared.HasValue.ShouldBeTrue();
        declared.Value.GetProperty("detail").GetString().ShouldBe("Reference<int>");
        declared.Value.GetProperty("kind").GetInt32()
            .ShouldBe((int)LanguageCompletionItemKind.Field);

        foreach (var message in messages)
        {
            message.Dispose();
        }
    }

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
