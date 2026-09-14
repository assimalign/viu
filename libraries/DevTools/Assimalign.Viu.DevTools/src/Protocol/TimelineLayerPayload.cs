namespace Assimalign.Viu.DevTools;

/// <summary>The timeline layer payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Identifier">The identity scoped to the connected runtime session.</param>
/// <param name="DisplayName">The human-readable registered name.</param>
/// <param name="Color">The optional display color.</param>
public sealed record TimelineLayerPayload(
    string Identifier,
    string DisplayName,
    string? Color);
