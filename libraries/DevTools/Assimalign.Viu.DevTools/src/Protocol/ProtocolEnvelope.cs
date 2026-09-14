using System.Text.Json;

namespace Assimalign.Viu.DevTools;

/// <summary>The protocol envelope exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Version">The protocol or recorded dependency version.</param>
/// <param name="Type">The case-sensitive message type.</param>
/// <param name="Payload">The JSON payload parsed through generated metadata.</param>
public sealed record ProtocolEnvelope(
    int Version,
    string Type,
    JsonElement Payload);
