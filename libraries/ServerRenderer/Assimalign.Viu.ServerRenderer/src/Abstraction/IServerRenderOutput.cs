using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Receives progressive server-rendered markup and exposes the host's flush boundary without
/// introducing a web-framework response type.
/// </summary>
/// <remarks>
/// The renderer borrows the output and never closes or disposes it. Each flush is awaited so the
/// host's own buffering and backpressure policy remains authoritative. Specified by
/// <c>[SSR-1]</c>, <c>[SSR-8]</c>, <c>[SSR-11]</c>, and <c>[SSR-12]</c>.
/// </remarks>
public interface IServerRenderOutput
{
    /// <summary>
    /// Gets whether the host response has crossed the point where it can no longer be wholly
    /// replaced.
    /// </summary>
    /// <remarks>
    /// The value is host-authoritative and monotonic. <see langword="false"/> guarantees that the
    /// host can still discard accepted content and send a clean replacement response; an attempted
    /// write or flush does not make the value true unless the host's transport actually commits.
    /// Reading this property performs no input/output and does not throw. The explicit signal keeps
    /// response policy at the hosting boundary. Specified by <c>[SSR-12]</c>.
    /// </remarks>
    bool ResponseCommitted { get; }

    /// <summary>Writes one non-empty serialized markup chunk to the host response.</summary>
    /// <param name="content">The serialized character content.</param>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>A value task that completes when the host accepts the content.</returns>
    ValueTask WriteAsync(
        ReadOnlyMemory<char> content,
        CancellationToken cancellationToken = default);

    /// <summary>Flushes accepted content through the host's backpressure boundary.</summary>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>A value task that completes only when the host completes the flush.</returns>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
