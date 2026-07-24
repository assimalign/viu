using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.LanguageServer;

internal sealed class LanguageServerProtocolMessageReader
{
    private const int MaximumHeaderLength = 64 * 1024;
    private const int MaximumPayloadLength = 16 * 1024 * 1024;

    private readonly Stream input;

    internal LanguageServerProtocolMessageReader(Stream input)
        => this.input = input ?? throw new ArgumentNullException(nameof(input));

    internal async ValueTask<JsonDocument?> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var header = new MemoryStream();
        var terminatorMatchLength = 0;

        while (true)
        {
            var next = new byte[1];
            var count = await input.ReadAsync(next, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                if (header.Length == 0)
                {
                    return null;
                }

                throw new EndOfStreamException("The language-server message header ended unexpectedly.");
            }

            header.WriteByte(next[0]);
            if (header.Length > MaximumHeaderLength)
            {
                throw new InvalidDataException("The language-server message header is too large.");
            }

            terminatorMatchLength = AdvanceHeaderTerminator(next[0], terminatorMatchLength);
            if (terminatorMatchLength == 4)
            {
                break;
            }
        }

        var headerBytes = header.ToArray();
        var headerText = Encoding.ASCII.GetString(headerBytes, 0, headerBytes.Length - 4);
        var contentLength = ParseContentLength(headerText);
        if (contentLength > MaximumPayloadLength)
        {
            throw new InvalidDataException("The language-server message payload is too large.");
        }

        var payload = new byte[contentLength];
        var received = 0;
        while (received < payload.Length)
        {
            var count = await input
                .ReadAsync(payload.AsMemory(received), cancellationToken)
                .ConfigureAwait(false);
            if (count == 0)
            {
                throw new EndOfStreamException("The language-server message payload ended unexpectedly.");
            }

            received += count;
        }

        return JsonDocument.Parse(payload);
    }

    private static int AdvanceHeaderTerminator(byte value, int currentMatchLength)
        => (currentMatchLength, value) switch
        {
            (0, (byte)'\r') => 1,
            (1, (byte)'\n') => 2,
            (2, (byte)'\r') => 3,
            (3, (byte)'\n') => 4,
            (_, (byte)'\r') => 1,
            _ => 0,
        };

    private static int ParseContentLength(string headerText)
    {
        foreach (var line in headerText.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0 ||
                !line.AsSpan(0, separator).Trim().Equals(
                    "Content-Length",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(line.AsSpan(separator + 1).Trim(), out var contentLength) &&
                contentLength >= 0)
            {
                return contentLength;
            }

            throw new InvalidDataException("The Content-Length header is invalid.");
        }

        throw new InvalidDataException("The Content-Length header is required.");
    }
}
