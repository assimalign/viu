namespace Assimalign.Viu.DevTools;

/// <summary>The inspector state request payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="InspectorIdentifier">The stable custom-inspector identity.</param>
/// <param name="NodeIdentifier">The inspector-defined node identity.</param>
/// <param name="Depth">The requested expansion depth, clamped by the runtime.</param>
public sealed record InspectorStateRequestPayload(
    string InspectorIdentifier,
    string NodeIdentifier,
    int Depth);
