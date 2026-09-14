namespace Assimalign.Viu.DevTools;

/// <summary>The component snapshot request payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Depth">The requested expansion depth, clamped by the runtime.</param>
public sealed record ComponentSnapshotRequestPayload(int Identifier, int Depth);
