using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Carries per-render teleport output and free-form state handed from server application code to
/// the surrounding document host.
/// </summary>
/// <remarks>
/// One instance belongs to one render and is not thread-safe. The renderer never interprets or
/// serializes <see cref="State"/> itself. Specified by <c>[SSR-7]</c>,
/// <c>[SSR-MARKERS-2]</c>, and <c>[HYD-6]</c>.
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

    /// <summary>Gets the deliberately unschematized per-render state handoff bag.</summary>
    /// <remarks>The renderer leaves values untouched as required by <c>[SSR-7]</c>.</remarks>
    public IDictionary<string, object?> State { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

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
