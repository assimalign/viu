using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The component tree node payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Name">The component, event, or member name.</param>
/// <param name="Key">The safely displayed reconciliation key.</param>
/// <param name="Children">The children at the requested depth.</param>
public sealed record ComponentTreeNodePayload(
    int Identifier,
    string Name,
    string? Key,
    List<ComponentTreeNodePayload> Children);
