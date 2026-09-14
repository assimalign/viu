namespace Assimalign.Viu.DevTools;

/// <summary>The component reorder payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="Index">The zero-based sibling position.</param>
public sealed record ComponentReorderPayload(int Identifier, int Index);
