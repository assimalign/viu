using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Connects a hosted browser panel to a WebSocket endpoint, reconnecting after closure. Specified by <c>[DVT-14]</c>.</summary>
/// <remarks>Single-threaded. Each open requests a new handshake; disposal cancels retry timers and closes the socket.</remarks>
[SupportedOSPlatform("browser")]
public sealed class WebSocketDevToolsClientTransport : IDevToolsClientTransport
{
    private readonly BrowserClientBridge _bridge;
    /// <summary>Creates a browser transport for an absolute ws or wss endpoint.</summary>
    public WebSocketDevToolsClientTransport(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("A ws or wss endpoint is required.", nameof(endpoint));
        }

        _bridge = new(endpoint.AbsoluteUri, true);
    }
    /// <inheritdoc/>
    public ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default) => _bridge.StartAsync(receiver, connected, cancellationToken);
    /// <inheritdoc/>
    public ValueTask SendAsync(string message, CancellationToken cancellationToken = default) => _bridge.SendAsync(message, cancellationToken);
    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _bridge.DisposeAsync();
}
