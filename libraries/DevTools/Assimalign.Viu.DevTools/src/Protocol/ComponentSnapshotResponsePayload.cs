using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>The component snapshot response payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Parameters">The current encoded component parameters.</param>
/// <param name="State">The current encoded inspectable state.</param>
/// <param name="Events">The declared event metadata.</param>
/// <param name="Error">The diagnostic message when data is unavailable.</param>
public sealed record ComponentSnapshotResponsePayload(
    int Identifier,
    List<DevToolsNamedValuePayload> Parameters,
    List<DevToolsNamedValuePayload> State,
    List<ComponentEventMetadataPayload> Events,
    string? Error);
