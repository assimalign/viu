using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Reads Language Server Protocol base-protocol frames (<c>Content-Length</c> header block, blank
/// line, UTF-8 JSON payload) from one stream. The reader owns a read-ahead buffer, so the header
/// is scanned byte by byte in memory rather than with one stream read per byte, and bytes that
/// belong to the next frame are kept for the next call. Because of that read-ahead, exactly one
/// reader may consume a given stream. Not thread-safe: calls to <see cref="ReadAsync"/> must not
/// overlap. Base-protocol reference:
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#baseProtocol"/>.
/// </summary>
internal sealed class LanguageServerProtocolMessageReader
{
    private const int MaximumHeaderLength = 64 * 1024;
    private const int MaximumPayloadLength = 16 * 1024 * 1024;
    private const int ReadAheadLength = 8 * 1024;

    private readonly Stream input;
    private readonly byte[] readAhead = new byte[ReadAheadLength];
    private byte[] header = new byte[256];
    private int readAheadOffset;
    private int readAheadCount;

    internal LanguageServerProtocolMessageReader(Stream input)
        => this.input = input ?? throw new ArgumentNullException(nameof(input));

    internal async ValueTask<JsonDocument?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var headerLength = 0;
        var terminatorMatchLength = 0;

        while (true)
        {
            if (readAheadOffset == readAheadCount && !await FillReadAheadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (headerLength == 0)
                {
                    return null;
                }

                throw new EndOfStreamException("The language-server message header ended unexpectedly.");
            }

            var next = readAhead[readAheadOffset++];
            if (headerLength == header.Length)
            {
                Array.Resize(ref header, Math.Min(header.Length * 2, MaximumHeaderLength + 1));
            }

            header[headerLength++] = next;
            if (headerLength > MaximumHeaderLength)
            {
                throw new InvalidDataException("The language-server message header is too large.");
            }

            terminatorMatchLength = AdvanceHeaderTerminator(next, terminatorMatchLength);
            if (terminatorMatchLength == 4)
            {
                break;
            }
        }

        var headerText = Encoding.ASCII.GetString(header, 0, headerLength - 4);
        var contentLength = ParseContentLength(headerText);
        if (contentLength > MaximumPayloadLength)
        {
            throw new InvalidDataException("The language-server message payload is too large.");
        }

        var payload = new byte[contentLength];
        var received = Math.Min(readAheadCount - readAheadOffset, contentLength);
        if (received > 0)
        {
            Array.Copy(readAhead, readAheadOffset, payload, 0, received);
            readAheadOffset += received;
        }

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

    private async ValueTask<bool> FillReadAheadAsync(CancellationToken cancellationToken)
    {
        readAheadOffset = 0;
        readAheadCount = await input.ReadAsync(readAhead, cancellationToken).ConfigureAwait(false);
        return readAheadCount > 0;
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
