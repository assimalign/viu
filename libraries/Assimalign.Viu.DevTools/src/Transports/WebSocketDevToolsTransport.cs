using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

/// <summary>Carries complete JSON protocol batches as WebSocket text messages.</summary>
/// <remarks>
/// Receive buffering is bounded, messages are delivered serially, and each outbound DevTools
/// batch maps to exactly one end-of-message WebSocket send. Specified by <c>[DVT-2]</c> and
/// <c>[DVT-6]</c>.
/// </remarks>
public sealed class WebSocketDevToolsTransport : IDevToolsTransport
{
    private const int ReceiveBufferSize = 4096;
    private readonly WebSocket _webSocket;
    private readonly bool _ownsWebSocket;
    private readonly int _maximumInboundMessageBytes;
    private readonly CancellationTokenSource _receiveCancellation = new();
    private Task? _receiveTask;
    private bool _disposed;

    /// <summary>Initializes a transport over an already-open WebSocket.</summary>
    /// <param name="webSocket">The connected socket.</param>
    /// <param name="ownsWebSocket">Whether disposal also disposes the supplied socket.</param>
    /// <param name="maximumInboundMessageBytes">The maximum accepted complete text message size.</param>
    public WebSocketDevToolsTransport(
        WebSocket webSocket,
        bool ownsWebSocket = false,
        int maximumInboundMessageBytes = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(webSocket);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumInboundMessageBytes, 1);
        _webSocket = webSocket;
        _ownsWebSocket = ownsWebSocket;
        _maximumInboundMessageBytes = maximumInboundMessageBytes;
    }

    /// <summary>Connects a client WebSocket and returns its owning transport.</summary>
    /// <param name="endpoint">The <c>ws</c> or <c>wss</c> diagnostic endpoint.</param>
    /// <param name="cancellationToken">Cancels connection establishment.</param>
    /// <returns>A transport that owns the newly connected socket.</returns>
    public static async ValueTask<WebSocketDevToolsTransport> ConnectAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ClientWebSocket socket = new();
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return new WebSocketDevToolsTransport(socket, ownsWebSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(receiver);
        cancellationToken.ThrowIfCancellationRequested();
        if (_receiveTask is not null)
        {
            return ValueTask.CompletedTask;
        }

        if (_webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("The WebSocket must be open before the transport starts.");
        }

        _receiveTask = ReceiveLoopAsync(receiver, _receiveCancellation.Token);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask SendAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _receiveCancellation.Cancel();
        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
        }

        if (_ownsWebSocket)
        {
            _webSocket.Dispose();
        }

        _receiveCancellation.Dispose();
    }

    private async Task ReceiveLoopAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[ReceiveBufferSize];
        using MemoryStream message = new();
        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                if (result.EndOfMessage)
                {
                    message.SetLength(0);
                }

                continue;
            }

            if (message.Length + result.Count > _maximumInboundMessageBytes)
            {
                await _webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    "The DevTools protocol frame exceeded its configured limit.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            string text = Encoding.UTF8.GetString(
                message.GetBuffer(),
                0,
                checked((int)message.Length));
            message.SetLength(0);
            await receiver(text).ConfigureAwait(false);
        }
    }
}
