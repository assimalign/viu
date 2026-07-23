using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The single character sink every node serializes into — the C# realization of the <c>push</c>
/// function threaded through <c>@vue/server-renderer</c>'s render pipeline
/// (<c>packages/server-renderer/src/render.ts</c>). One <see cref="StringBuilder"/> is threaded
/// through the whole render (never per-node string concatenation, the throughput reason the area
/// exists): <see cref="Append(string)"/> is the hot path and never allocates beyond the builder's own
/// growth. In string mode (<c>sink</c> null) the builder accumulates the whole document,
/// returned by <see cref="ToStringResult"/>. In streaming mode the builder is a bounded chunk buffer:
/// <see cref="FlushAsync"/> drains it to the backing <see cref="TextWriter"/> and awaits its
/// <see cref="TextWriter.FlushAsync()"/>, so the caller's write cadence applies backpressure and the
/// full document is never buffered. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal sealed class SsrWriter
{
    private readonly StringBuilder _builder = new();
    private readonly TextWriter? _sink;

    /// <summary>Creates an accumulating writer whose content is retrieved with <see cref="ToStringResult"/>.</summary>
    public SsrWriter()
    {
    }

    /// <summary>Creates a streaming writer that drains to <paramref name="sink"/> on each <see cref="FlushAsync"/>.</summary>
    /// <param name="sink">The destination writer.</param>
    public SsrWriter(TextWriter sink)
    {
        _sink = sink;
    }

    /// <summary>Whether this writer streams to a backing <see cref="TextWriter"/> (as opposed to accumulating).</summary>
    public bool IsStreaming => _sink is not null;

    /// <summary>Appends a serialized chunk (upstream: <c>push(string)</c>). The hot path — no I/O.</summary>
    /// <param name="chunk">The already-escaped HTML fragment.</param>
    public void Append(string chunk) => _builder.Append(chunk);

    /// <summary>
    /// Drains buffered content to the backing writer and awaits its flush (upstream: the point where an
    /// unrolled buffer segment is written). A no-op for an accumulating writer, so the same call sites
    /// serve both modes.
    /// </summary>
    /// <param name="cancellationToken">Cancels a pending write/flush.</param>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_sink is null || _builder.Length == 0)
        {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        await _sink.WriteAsync(_builder.ToString().AsMemory(), cancellationToken).ConfigureAwait(false);
        _builder.Clear();
        await _sink.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the accumulated content (string mode and teleport buffers).</summary>
    public string ToStringResult() => _builder.ToString();
}
