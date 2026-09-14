using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Assimalign.Viu.DevTools;

/// <summary>Creates and reads protocol-only messages using explicit generated JSON metadata. Specified by <c>[DVT-2]</c> and <c>[DVT-14]</c>.</summary>
public static class ProtocolCodec
{
    /// <summary>Encodes one payload without runtime contract discovery.</summary>
    /// <typeparam name="TPayload">The shared protocol payload type.</typeparam>
    /// <param name="type">The wire message type.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="typeInformation">The source-generated payload metadata.</param>
    /// <returns>A version 1 envelope containing detached JSON.</returns>
    public static ProtocolEnvelope CreateEnvelope<TPayload>(
        string type,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInformation) => new(
            DevToolsProtocol.CurrentVersion,
            type,
            JsonSerializer.SerializeToElement(payload, typeInformation));

    /// <summary>Serializes one complete batch preserving message order.</summary>
    /// <param name="messages">The envelopes in wire order.</param>
    /// <returns>The complete JSON transport frame.</returns>
    public static string SerializeBatch(List<ProtocolEnvelope> messages) =>
        JsonSerializer.Serialize(
            new ProtocolBatch(DevToolsProtocol.Name, messages),
            DevToolsJsonSerializerContext.Default.ProtocolBatch);

    /// <summary>Parses one complete batch without interpreting unknown envelope types.</summary>
    /// <param name="message">The complete JSON transport frame.</param>
    /// <returns>The parsed batch, or null for a JSON null value.</returns>
    /// <exception cref="JsonException">The frame is malformed or cannot represent a batch.</exception>
    public static ProtocolBatch? DeserializeBatch(string message) =>
        JsonSerializer.Deserialize(message, DevToolsJsonSerializerContext.Default.ProtocolBatch);
}
