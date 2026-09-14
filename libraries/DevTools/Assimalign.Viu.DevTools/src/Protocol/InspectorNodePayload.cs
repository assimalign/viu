using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The inspector node payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Label">The safely encoded display label.</param>
/// <param name="Children">The children at the requested depth.</param>
public sealed record InspectorNodePayload(
    string Identifier,
    string Label,
    List<InspectorNodePayload> Children);
