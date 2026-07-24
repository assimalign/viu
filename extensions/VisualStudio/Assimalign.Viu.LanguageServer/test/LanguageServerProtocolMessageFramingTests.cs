using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer;

public class LanguageServerProtocolMessageFramingTests
{
    [Fact]
    public async Task WriteAsync_UnicodePayload_UsesByteAccurateContentLengthReadableByMessageReader()
    {
        await using var stream = new MemoryStream();
        var writer = new LanguageServerProtocolMessageWriter(stream);

        await writer.WriteAsync(
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "test",
                ["params"] = new JsonObject
                {
                    ["value"] = "Viu café",
                },
            });

        stream.Position = 0;
        var reader = new LanguageServerProtocolMessageReader(stream);
        using var message = await reader.ReadAsync();

        message.ShouldNotBeNull();
        message!.RootElement
            .GetProperty("params")
            .GetProperty("value")
            .GetString()
            .ShouldBe("Viu café");
    }

    [Fact]
    public async Task ReadAsync_SeveralFrames_ReadsExactlyOnePayloadAtATime()
    {
        var bytes = Encoding.UTF8.GetBytes(
            Frame("""{"jsonrpc":"2.0","id":1,"method":"first"}""") +
            Frame("""{"jsonrpc":"2.0","id":2,"method":"second"}"""));
        await using var stream = new MemoryStream(bytes);
        var reader = new LanguageServerProtocolMessageReader(stream);

        using var first = await reader.ReadAsync();
        using var second = await reader.ReadAsync();

        first!.RootElement.GetProperty("method").GetString().ShouldBe("first");
        second!.RootElement.GetProperty("method").GetString().ShouldBe("second");
    }

    private static string Frame(string payload)
        => $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n\r\n{payload}";
}
