using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The per-render server context — the C# port of <c>SSRContext</c> in <c>@vue/server-renderer</c>
/// (<c>packages/server-renderer/src/renderToString.ts</c>,
/// https://vuejs.org/api/ssr.html#rendertostring). One instance is threaded through a single
/// <see cref="ServerRenderer.RenderToStringAsync(ServerApplication, SsrContext?, System.Threading.CancellationToken)"/>
/// (or streaming) call and carries the two things the surrounding document assembly needs after the
/// component tree serializes: the <see cref="Teleports"/> map (content that was rendered out of tree
/// position) and a free-form <see cref="State"/> bag for application handoff (e.g. serialized store
/// state the client picks up). A context belongs to exactly one render — reusing one across
/// concurrent renders would cross request state, defeating the per-request discipline the area
/// requires. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class SsrContext
{
    private readonly Dictionary<string, string> _teleports = new(StringComparer.Ordinal);
    private Dictionary<string, StringBuilder>? _teleportBuffers;

    /// <summary>
    /// The teleported content, keyed by the target selector the <c>&lt;Teleport&gt;</c>'s <c>to</c> prop
    /// named (upstream: <c>context.teleports</c>). Populated when the render completes: each entry is the
    /// fully serialized HTML for one target, ready for the host to splice into the target element. Empty
    /// when the tree contains no teleports. Mirrors Vue's <c>ssrContext.teleports</c> contract, including
    /// the trailing <c>&lt;!--teleport anchor--&gt;</c> the hydration walker expects.
    /// </summary>
    public IReadOnlyDictionary<string, string> Teleports => _teleports;

    /// <summary>
    /// A free-form state bag for the render (upstream: <c>SSRContext</c> is an open <c>Record</c>).
    /// Application code and helpers stash per-request data here — most importantly the serialized state
    /// the client rehydrates — without the renderer prescribing a shape. Never used by the renderer's
    /// own HTML output.
    /// </summary>
    public IDictionary<string, object?> State { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Appends <paramref name="content"/> to the buffer for <paramref name="target"/> (upstream:
    /// <c>ssrRenderTeleport</c> pushing into <c>context.__teleportBuffers[target]</c>). Multiple teleports
    /// naming the same target accumulate in tree order; the buffer is resolved to a
    /// <see cref="Teleports"/> entry by <see cref="ResolveTeleports"/> when the render completes.
    /// </summary>
    /// <param name="target">The target selector from the teleport's <c>to</c> prop.</param>
    /// <param name="content">The serialized teleport content (children plus the anchor marker).</param>
    internal void AppendTeleport(string target, string content)
    {
        _teleportBuffers ??= new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        if (!_teleportBuffers.TryGetValue(target, out var builder))
        {
            builder = new StringBuilder();
            _teleportBuffers[target] = builder;
        }
        builder.Append(content);
    }

    /// <summary>
    /// Freezes the accumulated teleport buffers into the public <see cref="Teleports"/> map (upstream:
    /// <c>resolveTeleports</c>). Called once, after the whole component tree has serialized, so a teleport
    /// that appears anywhere in the tree is captured.
    /// </summary>
    internal void ResolveTeleports()
    {
        if (_teleportBuffers is null)
        {
            return;
        }
        foreach (var (target, builder) in _teleportBuffers)
        {
            _teleports[target] = builder.ToString();
        }
    }
}
