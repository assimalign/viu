using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Provides the push-based write surface shared by runtime traversal and compiler-produced server
/// render bodies.
/// </summary>
/// <remarks>
/// Callers escape before pushing; raw static markup and hydration markers therefore pass through
/// unchanged. Instances are renderer-owned and not thread-safe. Specified by <c>[SSR-1]</c>,
/// <c>[SSR-3]</c>, and <c>[SSR-6]</c>.
/// </remarks>
public sealed class SsrRenderState
{
    private readonly SsrWriter _writer;

    internal SsrRenderState(
        SsrWriter writer,
        SsrContext context,
        IApplicationContext application,
        ComponentHost componentHost,
        CancellationToken cancellationToken,
        IServerRenderRegistry? serverRenders = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(componentHost);
        _writer = writer;
        Context = context;
        Application = application;
        ComponentHost = componentHost;
        CancellationToken = cancellationToken;
        ServerRenders = serverRenders;
    }

    /// <summary>Gets the render context visible to this serialization state.</summary>
    /// <remarks>
    /// Ordinary traversal exposes the request-owned context. A registry-selected direct-markup
    /// body instead sees a transaction-local child context whose teleport and state contributions
    /// commit to the request context only after the body succeeds. Specified by <c>[SSR-7]</c> and
    /// <c>[SSR-TARGET-3]</c>.
    /// </remarks>
    public SsrContext Context { get; }

    /// <summary>Gets the immutable borrowed application composition for this render.</summary>
    /// <remarks>Specified by <c>[SSR-2]</c> and <c>[SSR-9]</c>.</remarks>
    public IApplicationContext Application { get; }

    /// <summary>Gets cancellation observed by prefetch, traversal, writing, and flushing.</summary>
    /// <remarks>Specified by <c>[SSR-5]</c>.</remarks>
    public CancellationToken CancellationToken { get; }

    internal ComponentHost ComponentHost { get; }

    internal IServerRenderRegistry? ServerRenders { get; }

    /// <summary>Appends an already escaped or deliberately raw HTML fragment.</summary>
    /// <param name="chunk">The non-null serialized fragment.</param>
    /// <remarks>This method performs no transformation under <c>[SSR-6]</c>.</remarks>
    public void Push(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Length > 0)
        {
            _writer.Append(chunk);
        }
    }

    /// <summary>Requests a streaming boundary for the current chunk.</summary>
    /// <returns>
    /// A task completing after a direct-markup transaction records the boundary, after the backing
    /// writer flushes for ordinary streaming, or immediately in string mode. Recorded boundaries
    /// are replayed after the direct body succeeds and before later chunks are committed.
    /// </returns>
    /// <remarks>
    /// Deferred transaction boundaries preserve ordering and destination backpressure without
    /// exposing output from a body that may still fail. Specified by <c>[SSR-1]</c> and
    /// <c>[SSR-TARGET-3]</c>.
    /// </remarks>
    public Task FlushAsync() => _writer.FlushAsync(CancellationToken);

    internal SsrRenderState CreateBuffer(SsrWriter writer) =>
        new(writer, Context, Application, ComponentHost, CancellationToken, ServerRenders);

    internal SsrRenderState CreateBuffer(SsrWriter writer, SsrContext context) =>
        new(writer, context, Application, ComponentHost, CancellationToken, ServerRenders);
}
