using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins the base-protocol framing the reader accepts. Compatibility target: the Language Server
/// Protocol base protocol,
/// https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#baseProtocol.
/// </summary>
public sealed class LanguageServerProtocolMessageReaderTests
{
    [Fact]
    public async Task ReadAsync_TwoFramesArrivingTogether_ReturnsEachMessageThenNullAtEndOfStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            Frame("""{"jsonrpc":"2.0","id":1,"method":"first"}""") +
            Frame("""{"jsonrpc":"2.0","id":2,"method":"second"}""")));
        var reader = new LanguageServerProtocolMessageReader(stream);

        using JsonDocument? first = await reader.ReadAsync();
        using JsonDocument? second = await reader.ReadAsync();
        JsonDocument? end = await reader.ReadAsync();

        first.ShouldNotBeNull().RootElement.GetProperty("method").GetString().ShouldBe("first");
        second.ShouldNotBeNull().RootElement.GetProperty("method").GetString().ShouldBe("second");
        end.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_StreamDeliveringOneByteAtATime_ReassemblesEveryFrame()
    {
        using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(
            Frame("""{"jsonrpc":"2.0","id":1,"method":"first"}""") +
            Frame("""{"jsonrpc":"2.0","id":2,"method":"second"}""")), chunkSize: 1);
        var reader = new LanguageServerProtocolMessageReader(stream);

        using JsonDocument? first = await reader.ReadAsync();
        using JsonDocument? second = await reader.ReadAsync();

        first.ShouldNotBeNull().RootElement.GetProperty("id").GetInt32().ShouldBe(1);
        second.ShouldNotBeNull().RootElement.GetProperty("id").GetInt32().ShouldBe(2);
        (await reader.ReadAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_PayloadLargerThanTheReadAheadBuffer_ReadsTheCompletePayload()
    {
        string text = new('x', 40_000);
        string json = $$$"""{"jsonrpc":"2.0","id":7,"params":{"text":"{{{text}}}"}}""";
        using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(Frame(json) + Frame("""{"id":8}""")), chunkSize: 3_000);
        var reader = new LanguageServerProtocolMessageReader(stream);

        using JsonDocument? large = await reader.ReadAsync();
        using JsonDocument? next = await reader.ReadAsync();

        large.ShouldNotBeNull().RootElement.GetProperty("params").GetProperty("text").GetString().ShouldBe(text);
        next.ShouldNotBeNull().RootElement.GetProperty("id").GetInt32().ShouldBe(8);
    }

    [Fact]
    public async Task ReadAsync_HeaderWithAdditionalFieldsAndMixedCaseName_UsesContentLength()
    {
        const string json = """{"id":1}""";
        string frame =
            "Content-Type: application/vscode-jsonrpc; charset=utf-8\r\n" +
            $"content-length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n" +
            json;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame));
        var reader = new LanguageServerProtocolMessageReader(stream);

        using JsonDocument? message = await reader.ReadAsync();

        message.ShouldNotBeNull().RootElement.GetProperty("id").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_HeaderWithoutContentLength_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content-Type: text/plain\r\n\r\n{}"));
        var reader = new LanguageServerProtocolMessageReader(stream);

        await Should.ThrowAsync<InvalidDataException>(async () => await reader.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_TruncatedPayload_ThrowsEndOfStreamException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content-Length: 50\r\n\r\n{\"id\":1}"));
        var reader = new LanguageServerProtocolMessageReader(stream);

        await Should.ThrowAsync<EndOfStreamException>(async () => await reader.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_TruncatedHeader_ThrowsEndOfStreamException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content-Length: 5\r\n"));
        var reader = new LanguageServerProtocolMessageReader(stream);

        await Should.ThrowAsync<EndOfStreamException>(async () => await reader.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_HeaderBeyondTheSizeLimit_ThrowsInvalidDataException()
    {
        byte[] header = new byte[70 * 1024];
        Array.Fill(header, (byte)'X');
        using var stream = new MemoryStream(header);
        var reader = new LanguageServerProtocolMessageReader(stream);

        await Should.ThrowAsync<InvalidDataException>(async () => await reader.ReadAsync());
    }

    private static string Frame(string json) =>
        $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";

    /// <summary>A read-only stream that hands out at most <c>chunkSize</c> bytes per read.</summary>
    private sealed class ChunkedStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _chunkSize;
        private int _position;

        internal ChunkedStream(byte[] data, int chunkSize)
        {
            _data = data;
            _chunkSize = chunkSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int count = Math.Min(Math.Min(buffer.Length, _chunkSize), _data.Length - _position);
            _data.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer, offset, count));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
