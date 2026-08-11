using System;

namespace Assimalign.Viu.DevTools;

/// <summary>
/// Declares one named custom timeline layer without emitting timeline events.
/// </summary>
/// <remarks>
/// Event recording belongs to the later timeline work item; this release reserves only stable
/// layer identity and presentation metadata. Specified by <c>[DVT-7]</c>.
/// </remarks>
public sealed class DevToolsTimelineLayer
{
    /// <summary>Initializes timeline-layer registration metadata.</summary>
    /// <param name="identifier">The stable layer identifier.</param>
    /// <param name="displayName">The human-readable layer name.</param>
    /// <param name="color">An optional CSS color token used by a diagnostic client.</param>
    public DevToolsTimelineLayer(
        string identifier,
        string displayName,
        string? color = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        Identifier = identifier;
        DisplayName = displayName;
        Color = color;
    }

    /// <summary>Gets the stable layer identifier.</summary>
    public string Identifier { get; }

    /// <summary>Gets the human-readable layer name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the optional CSS color token.</summary>
    public string? Color { get; }
}
