using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Carries per-render teleport output and the versioned state-store payload handed to the
/// surrounding document host.
/// </summary>
/// <remarks>
/// One request-owned instance carries the committed render result and is not thread-safe. A
/// registry-selected direct-markup body receives a child transaction initialized from that
/// instance; its teleport and state contributions merge into the request context only after the
/// body succeeds. Store state is captured only through explicit serializers after component
/// traversal, then emitted through the inert state island. Specified by <c>[SSR-7]</c>,
/// <c>[SSR-MARKERS-2]</c>, <c>[HYD-6]</c>, <c>[STA-9]</c>, and <c>[SSR-TARGET-3]</c>.
/// </remarks>
public sealed class SsrContext
{
    private readonly Dictionary<string, string> _teleports = new(StringComparer.Ordinal);
    private Dictionary<string, StringBuilder>? _teleportBuffers;

    /// <summary>
    /// Gets fully serialized out-of-tree content by target identifier after rendering completes.
    /// </summary>
    /// <remarks>
    /// Each contribution ends in <see cref="HydrationMarkers.TeleportAnchor"/> and must be spliced
    /// verbatim before hydration. Specified by <c>[SSR-MARKERS-2]</c> and <c>[HYD-6]</c>.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Teleports => _teleports;

    /// <summary>
    /// Gets the versioned payload captured from the request's materialized state stores, or
    /// <see langword="null"/> when the application has no payload-capable state registry.
    /// </summary>
    /// <remarks>
    /// The payload schema is <c>{"version":1,"stores":{"store-key":state}}</c>. Values are
    /// produced by each definition's explicit serializer and are safe to embed in a JSON island.
    /// Specified by <c>[SSR-7]</c> and <c>[STA-9]</c>; constrained by <c>[EXE-4]</c>.
    /// </remarks>
    public StateStorePayload? State { get; internal set; }

    internal void AppendTeleport(string target, string content)
    {
        _teleportBuffers ??= new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        if (!_teleportBuffers.TryGetValue(target, out StringBuilder? builder))
        {
            builder = new StringBuilder();
            _teleportBuffers[target] = builder;
        }

        builder.Append(content);
    }

    internal SsrContext CreateRenderTransaction() => new()
    {
        State = State,
    };

    internal void CommitRenderTransaction(SsrContext transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        transaction.ResolveTeleports();
        foreach (KeyValuePair<string, string> teleport in transaction._teleports)
        {
            AppendTeleport(teleport.Key, teleport.Value);
        }

        State = transaction.State;
    }

    internal void ResolveTeleports()
    {
        if (_teleportBuffers is null)
        {
            return;
        }

        foreach (KeyValuePair<string, StringBuilder> entry in _teleportBuffers)
        {
            _teleports[entry.Key] = entry.Value.ToString();
        }
    }
}
