using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools.Tests;

internal interface ITransportHarness
{
    IDevToolsTransport Transport { get; }

    IReadOnlyList<string> SentFrames { get; }

    ValueTask ReceiveAsync(string message);
}

internal static class TransportHarness
{
    internal static ITransportHarness Create(string kind) => kind switch
    {
        "postMessage" => new PostMessageTransportHarness(),
        "webSocket" => new WebSocketTransportHarness(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

internal sealed class PostMessageTransportHarness : ITransportHarness
{
    private readonly PostMessageBridgeProbe _bridge = new();

    internal PostMessageTransportHarness()
    {
#pragma warning disable CA1416 // The test injects a host-neutral bridge and performs no browser interop.
        Transport = new PostMessageDevToolsTransport(_bridge);
#pragma warning restore CA1416
    }

    public IDevToolsTransport Transport { get; }

    public IReadOnlyList<string> SentFrames => _bridge.SentFrames;

    public ValueTask ReceiveAsync(string message) => _bridge.ReceiveAsync(message);
}

internal sealed class PostMessageBridgeProbe : IPostMessageBridge
{
    private readonly List<string> _sentFrames = [];
    private Func<string, ValueTask>? _receiver;

    internal IReadOnlyList<string> SentFrames
    {
        get
        {
            lock (_sentFrames)
            {
                return _sentFrames.ToArray();
            }
        }
    }

    public ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _receiver = receiver;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sentFrames)
        {
            _sentFrames.Add(message);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ReceiveAsync(string message) =>
        _receiver?.Invoke(message)
        ?? throw new InvalidOperationException("The bridge has not been started.");

    public ValueTask DisposeAsync()
    {
        _receiver = null;
        return ValueTask.CompletedTask;
    }
}

internal sealed class WebSocketTransportHarness : ITransportHarness
{
    private readonly WebSocketProbe _webSocket = new();

    internal WebSocketTransportHarness() =>
        Transport = new WebSocketDevToolsTransport(_webSocket, ownsWebSocket: true);

    public IDevToolsTransport Transport { get; }

    public IReadOnlyList<string> SentFrames => _webSocket.SentFrames;

    public ValueTask ReceiveAsync(string message)
    {
        _webSocket.ReceiveFromClient(message);
        return ValueTask.CompletedTask;
    }
}

internal sealed class WebSocketProbe : WebSocket
{
    private readonly Channel<byte[]> _received = Channel.CreateUnbounded<byte[]>();
    private readonly List<string> _sentFrames = [];
    private WebSocketCloseStatus? _closeStatus;
    private string? _closeStatusDescription;
    private WebSocketState _state = WebSocketState.Open;

    internal IReadOnlyList<string> SentFrames
    {
        get
        {
            lock (_sentFrames)
            {
                return _sentFrames.ToArray();
            }
        }
    }

    public override WebSocketCloseStatus? CloseStatus => _closeStatus;

    public override string? CloseStatusDescription => _closeStatusDescription;

    public override WebSocketState State => _state;

    public override string? SubProtocol => null;

    internal void ReceiveFromClient(string message) =>
        _received.Writer.TryWrite(Encoding.UTF8.GetBytes(message));

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
        _received.Writer.TryComplete();
    }

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;
        _state = WebSocketState.Closed;
        _received.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _state = WebSocketState.Closed;
        _received.Writer.TryComplete();
    }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        byte[] message = await _received.Reader.ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (message.Length > buffer.Count)
        {
            throw new InvalidOperationException("The test protocol frame exceeds the receive probe buffer.");
        }

        message.CopyTo(buffer.Array!, buffer.Offset);
        return new WebSocketReceiveResult(
            message.Length,
            WebSocketMessageType.Text,
            endOfMessage: true);
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sentFrames)
        {
            _sentFrames.Add(Encoding.UTF8.GetString(
                buffer.Array!,
                buffer.Offset,
                buffer.Count));
        }

        return Task.CompletedTask;
    }
}

internal sealed class RecordingTransport : IDevToolsTransport
{
    private readonly List<string> _sentFrames = [];
    private Func<string, ValueTask>? _receiver;

    internal IReadOnlyList<string> SentFrames
    {
        get
        {
            lock (_sentFrames)
            {
                return _sentFrames.ToArray();
            }
        }
    }

    public ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _receiver = receiver;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sentFrames)
        {
            _sentFrames.Add(message);
        }

        return ValueTask.CompletedTask;
    }

    internal ValueTask ReceiveAsync(string message) =>
        _receiver?.Invoke(message)
        ?? throw new InvalidOperationException("The transport has not been started.");

    public ValueTask DisposeAsync()
    {
        _receiver = null;
        return ValueTask.CompletedTask;
    }
}

internal sealed class SlowTransport : IDevToolsTransport
{
    private readonly TaskCompletionSource _releaseFirstSend = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _sendCount;
    private int _cancellationObserved;
    private Func<string, ValueTask>? _receiver;

    internal int SendCount => Volatile.Read(ref _sendCount);

    internal bool CancellationObserved => Volatile.Read(ref _cancellationObserved) != 0;

    internal List<string> SentFrames { get; } = [];

    public ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _receiver = receiver;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int send = Interlocked.Increment(ref _sendCount);
        lock (SentFrames)
        {
            SentFrames.Add(message);
        }

        return send == 1
            ? new ValueTask(AwaitFirstSendAsync(cancellationToken))
            : ValueTask.CompletedTask;
    }

    internal void ReleaseFirstSend() => _releaseFirstSend.TrySetResult();

    internal ValueTask ReceiveAsync(string message) =>
        _receiver?.Invoke(message)
        ?? throw new InvalidOperationException("The transport has not been started.");

    private async Task AwaitFirstSendAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _releaseFirstSend.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref _cancellationObserved, 1);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _receiver = null;
        _releaseFirstSend.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
