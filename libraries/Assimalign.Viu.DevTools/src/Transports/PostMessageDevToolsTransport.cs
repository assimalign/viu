using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

/// <summary>
/// Carries protocol batches through one browser <c>window.postMessage</c> crossing per batch.
/// </summary>
/// <remarks>
/// This browser-only adapter lives in the opt-in DevTools package because it depends only on the
/// web-messaging transport, not on the Browser DOM renderer. Keeping it here preserves one
/// optional package while Core's hook contracts remain host-neutral. The bridge filters inbound
/// messages by direction and target origin and removes its listener on disposal. Specified by
/// <c>[DVT-2]</c> and <c>[DVT-6]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class PostMessageDevToolsTransport : IDevToolsTransport
{
    private readonly IPostMessageBridge _bridge;
    private bool _started;
    private bool _disposed;

    internal PostMessageDevToolsTransport(IPostMessageBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        _bridge = bridge;
    }

    /// <summary>Creates the browser transport and loads its packaged JavaScript module.</summary>
    /// <param name="targetOrigin">
    /// The exact allowed web origin, or <c>*</c> when the embedding environment deliberately
    /// accepts every origin.
    /// </param>
    /// <param name="cancellationToken">Cancels module loading.</param>
    /// <returns>The initialized transport; call <see cref="StartAsync"/> through the session.</returns>
    public static async ValueTask<PostMessageDevToolsTransport> CreateAsync(
        string targetOrigin,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetOrigin);
        IPostMessageBridge bridge = await JavaScriptPostMessageBridge.CreateAsync(
            targetOrigin,
            cancellationToken).ConfigureAwait(false);
        return new PostMessageDevToolsTransport(bridge);
    }

    /// <inheritdoc/>
    public async ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(receiver);
        if (_started)
        {
            return;
        }

        await _bridge.StartAsync(receiver, cancellationToken).ConfigureAwait(false);
        _started = true;
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        if (!_started)
        {
            throw new InvalidOperationException("The postMessage transport has not been started.");
        }

        return _bridge.SendAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _bridge.DisposeAsync().ConfigureAwait(false);
    }
}
