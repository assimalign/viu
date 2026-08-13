using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

/// <summary>
/// Carries complete JSON protocol batches between one running Viu application and one diagnostic
/// client.
/// </summary>
/// <remarks>
/// Each call to <see cref="SendAsync"/> is one already-batched transport frame. Implementations
/// must deliver received frames serially and must not call the receiver from inside
/// <see cref="SendAsync"/>. Specified by <c>[DVT-2]</c> and <c>[DVT-6]</c>.
/// </remarks>
public interface IDevToolsTransport : IAsyncDisposable
{
    /// <summary>Starts inbound delivery to the supplied non-blocking receiver.</summary>
    /// <param name="receiver">The receiver for each complete UTF-8 JSON text frame.</param>
    /// <param name="cancellationToken">Cancels transport startup.</param>
    /// <returns>A value task that completes when inbound delivery is active.</returns>
    ValueTask StartAsync(
        Func<string, ValueTask> receiver,
        CancellationToken cancellationToken = default);

    /// <summary>Sends one complete JSON batch as one transport frame.</summary>
    /// <param name="message">The complete JSON batch.</param>
    /// <param name="cancellationToken">Cancels this send without cancelling the session.</param>
    /// <returns>A value task representing transport acceptance of the frame.</returns>
    ValueTask SendAsync(
        string message,
        CancellationToken cancellationToken = default);
}
