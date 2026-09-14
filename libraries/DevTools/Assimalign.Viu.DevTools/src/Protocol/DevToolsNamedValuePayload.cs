namespace Assimalign.Viu.DevTools;

/// <summary>The dev tools named value payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Name">The component, event, or member name.</param>
/// <param name="Value">The wire value at this path.</param>
public sealed record DevToolsNamedValuePayload(
    string Name,
    DevToolsValuePayload Value);
