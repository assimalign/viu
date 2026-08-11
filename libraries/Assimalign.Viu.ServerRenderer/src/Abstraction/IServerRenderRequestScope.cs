using System;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Owns the application composition and server-render context created for exactly one host
/// request.
/// </summary>
/// <remarks>
/// The server adaptor disposes this scope after rendering, cancellation, or failure. The scope,
/// application, and context must all be fresh for each factory invocation; the adaptor rejects
/// reuse. Specified by <c>[SSR-8]</c>, <c>[SSR-9]</c>, and <c>[SSR-11]</c>.
/// </remarks>
public interface IServerRenderRequestScope : IAsyncDisposable
{
    /// <summary>Gets the request-owned server application composition.</summary>
    ServerRenderApplication Application { get; }

    /// <summary>Gets the request-owned teleport and hydration-state handoff context.</summary>
    SsrContext RenderContext { get; }
}
