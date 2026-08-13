using System.Text.Json.Serialization;

namespace Assimalign.Viu.DevTools;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProtocolBatch))]
[JsonSerializable(typeof(ProtocolEnvelope))]
[JsonSerializable(typeof(HandshakeRequestPayload))]
[JsonSerializable(typeof(HandshakeResponsePayload))]
[JsonSerializable(typeof(EmptyPayload))]
[JsonSerializable(typeof(TelemetryDroppedPayload))]
[JsonSerializable(typeof(ComponentChangePayload))]
[JsonSerializable(typeof(ComponentIdentifierPayload))]
[JsonSerializable(typeof(ComponentReorderPayload))]
[JsonSerializable(typeof(ComponentEventPayload))]
[JsonSerializable(typeof(ComponentSnapshotRequestPayload))]
[JsonSerializable(typeof(ComponentSnapshotResponsePayload))]
[JsonSerializable(typeof(ComponentExpansionRequestPayload))]
[JsonSerializable(typeof(ComponentExpansionResponsePayload))]
[JsonSerializable(typeof(ComponentEventMetadataPayload))]
[JsonSerializable(typeof(ComponentTreeSnapshotPayload))]
[JsonSerializable(typeof(ComponentTreeNodePayload))]
[JsonSerializable(typeof(DevToolsNamedValuePayload))]
[JsonSerializable(typeof(DevToolsValuePayload))]
[JsonSerializable(typeof(InspectorRegistrationPayload))]
[JsonSerializable(typeof(InspectorIdentifierPayload))]
[JsonSerializable(typeof(InspectorTreeRequestPayload))]
[JsonSerializable(typeof(InspectorTreeResponsePayload))]
[JsonSerializable(typeof(InspectorNodePayload))]
[JsonSerializable(typeof(InspectorStateRequestPayload))]
[JsonSerializable(typeof(InspectorStateResponsePayload))]
[JsonSerializable(typeof(TimelineLayerPayload))]
internal sealed partial class DevToolsJsonSerializerContext : JsonSerializerContext;
