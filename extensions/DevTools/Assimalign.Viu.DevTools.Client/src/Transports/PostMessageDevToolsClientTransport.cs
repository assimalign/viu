using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Connects an embedded panel to its parent window, or a standalone panel to its own window. Specified by <c>[DVT-14]</c>.</summary>
/// <remarks>Single-threaded. Checks source, origin, and direction; removes listeners on disposal.</remarks>
[SupportedOSPlatform("browser")]
public sealed class PostMessageDevToolsClientTransport : IDevToolsClientTransport
{
    private readonly BrowserClientBridge _bridge;
    private PostMessageDevToolsClientTransport(string targetOrigin) => _bridge = new(targetOrigin, false);
    /// <summary>Creates a transport allowing the exact inspected-page origin, or an explicitly chosen wildcard.</summary>
    public static ValueTask<PostMessageDevToolsClientTransport> CreateAsync(string targetOrigin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetOrigin);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PostMessageDevToolsClientTransport(targetOrigin));
    }
    /// <inheritdoc/>
    public ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default) => _bridge.StartAsync(receiver, connected, cancellationToken);
    /// <inheritdoc/>
    public ValueTask SendAsync(string message, CancellationToken cancellationToken = default) => _bridge.SendAsync(message, cancellationToken);
    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _bridge.DisposeAsync();
}
