using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The inspector tree response payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="InspectorIdentifier">The stable custom-inspector identity.</param>
/// <param name="Roots">The root nodes in presentation order.</param>
/// <param name="Error">The diagnostic message when data is unavailable.</param>
public sealed record InspectorTreeResponsePayload(
    string InspectorIdentifier,
    List<InspectorNodePayload> Roots,
    string? Error);
