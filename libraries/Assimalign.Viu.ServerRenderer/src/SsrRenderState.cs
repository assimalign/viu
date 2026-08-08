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
        CancellationToken cancellationToken)
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
    }

    /// <summary>Gets the context shared by the root and every nested or teleported subtree.</summary>
    /// <remarks>Specified by <c>[SSR-7]</c>.</remarks>
    public SsrContext Context { get; }

    /// <summary>Gets the immutable borrowed application composition for this render.</summary>
    /// <remarks>Specified by <c>[SSR-2]</c> and <c>[SSR-9]</c>.</remarks>
    public IApplicationContext Application { get; }

    /// <summary>Gets cancellation observed by prefetch, traversal, writing, and flushing.</summary>
    /// <remarks>Specified by <c>[SSR-5]</c>.</remarks>
    public CancellationToken CancellationToken { get; }

    internal ComponentHost ComponentHost { get; }

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

    /// <summary>Flushes the current streaming chunk and awaits destination backpressure.</summary>
    /// <returns>A task completing when the backing writer has flushed, or immediately in string mode.</returns>
    /// <remarks>Completed component subtrees call this boundary under <c>[SSR-1]</c>.</remarks>
    public Task FlushAsync() => _writer.FlushAsync(CancellationToken);

    internal SsrRenderState CreateBuffer(SsrWriter writer) =>
        new(writer, Context, Application, ComponentHost, CancellationToken);
}
