namespace Assimalign.Viu.DevTools;

/// <summary>The component event metadata payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Name">The component, event, or member name.</param>
/// <param name="HasValidator">Whether an argument validator is declared.</param>
public sealed record ComponentEventMetadataPayload(
    string Name,
    bool HasValidator);
