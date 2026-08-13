using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Assimalign.Viu.DevTools;

internal static class ProtocolCodec
{
    internal static ProtocolEnvelope CreateEnvelope<TPayload>(
        string type,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInformation) => new(
            DevToolsProtocol.CurrentVersion,
            type,
            JsonSerializer.SerializeToElement(payload, typeInformation));

    internal static string SerializeBatch(List<ProtocolEnvelope> messages) =>
        JsonSerializer.Serialize(
            new ProtocolBatch(DevToolsProtocol.Name, messages),
            DevToolsJsonSerializerContext.Default.ProtocolBatch);
}
