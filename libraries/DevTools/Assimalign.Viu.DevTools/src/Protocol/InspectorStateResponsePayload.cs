using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The inspector state response payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="InspectorIdentifier">The stable custom-inspector identity.</param>
/// <param name="NodeIdentifier">The inspector-defined node identity.</param>
/// <param name="State">The current encoded inspectable state.</param>
/// <param name="Error">The diagnostic message when data is unavailable.</param>
public sealed record InspectorStateResponsePayload(
    string InspectorIdentifier,
    string NodeIdentifier,
    List<DevToolsNamedValuePayload> State,
    string? Error);
