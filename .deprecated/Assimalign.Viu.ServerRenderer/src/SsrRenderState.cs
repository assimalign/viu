using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The write surface threaded through a server render — the C# counterpart of the <c>push</c>
/// function plus the ambient <c>SSRContext</c> that <c>@vue/server-renderer</c> passes down its
/// pipeline (<c>packages/server-renderer/src/render.ts</c>). It is the object the vnode-walking
/// runtime renderer carries, and the same surface the compiler-generated <c>ssrRender</c> bodies
/// ([V01.01.07.02]) will receive, so both paths append to one buffer and share one
/// <see cref="Context"/>. Instances are created by the renderer; helpers and generated code receive
/// one and never construct it. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class SsrRenderState
{
    private readonly SsrWriter _writer;

    internal SsrRenderState(SsrWriter writer, SsrContext context, CancellationToken cancellationToken)
    {
        _writer = writer;
        Context = context;
        CancellationToken = cancellationToken;
    }

    /// <summary>The render's <see cref="SsrContext"/> (upstream: the ambient <c>SSRContext</c>).</summary>
    public SsrContext Context { get; }

    /// <summary>The cancellation token for the render, observed at write/flush boundaries.</summary>
    public CancellationToken CancellationToken { get; }

    internal SsrWriter Writer => _writer;

    /// <summary>
    /// Appends an already-escaped HTML fragment to the render buffer (upstream: <c>push(item)</c>).
    /// Callers escape before pushing — this method never transforms its input, so raw markup
    /// (<c>v-html</c>, static vnodes, comment markers) passes through verbatim.
    /// </summary>
    /// <param name="chunk">The serialized fragment.</param>
    public void Push(string chunk)
    {
        if (!string.IsNullOrEmpty(chunk))
        {
            _writer.Append(chunk);
        }
    }

    /// <summary>
    /// Flushes buffered content to the backing writer in streaming mode and awaits its flush, applying
    /// backpressure; a no-op when rendering to a string. The renderer calls this at component-subtree
    /// boundaries so completed subtrees stream out without buffering the whole document.
    /// </summary>
    public Task FlushAsync() => _writer.FlushAsync(CancellationToken);
}
