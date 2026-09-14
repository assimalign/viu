namespace Assimalign.Viu.DevTools;

/// <summary>The telemetry dropped payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Count">The number of lost observations.</param>
public sealed record TelemetryDroppedPayload(int Count);
