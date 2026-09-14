using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The component event payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Name">The component, event, or member name.</param>
/// <param name="Arguments">Safely encoded event arguments.</param>
public sealed record ComponentEventPayload(
    int Identifier,
    string Name,
    List<DevToolsValuePayload> Arguments);
