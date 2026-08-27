using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Owns the application composition and server-render context created for exactly one host
/// request.
/// </summary>
/// <remarks>
/// The server adaptor disposes this scope after rendering, cancellation, or failure. The scope,
/// application, and context must all be fresh for each factory invocation; the adaptor rejects
/// reuse. Specified by <c>[SSR-8]</c>, <c>[SSR-9]</c>, <c>[SSR-11]</c>, and <c>[SSR-13]</c>.
/// </remarks>
public interface IServerRenderRequestScope : IAsyncDisposable
{
    /// <summary>Gets the request-owned server application composition.</summary>
    ServerRenderApplication Application { get; }

    /// <summary>Gets the request-owned teleport and hydration-state handoff context.</summary>
    SsrContext RenderContext { get; }

    /// <summary>Asynchronously releases the request scope while observing host request abort.</summary>
    /// <param name="cancellationToken">The original host request cancellation token.</param>
    /// <returns>A value task completing after every request-owned resource has been released.</returns>
    /// <remarks>
    /// Implementations that can cancel teardown work override this member, but must still release
    /// all owned resources when cancellation is requested. The default implementation delegates to
    /// <see cref="IAsyncDisposable.DisposeAsync"/> so existing scopes retain their parameterless
    /// teardown behavior. The adaptor invokes this overload on every teardown path. Specified by
    /// <c>[SSR-13]</c>.
    /// </remarks>
    ValueTask DisposeAsync(CancellationToken cancellationToken) => DisposeAsync();
}
