using System.Text.Json.Serialization;

namespace Assimalign.Viu.DevTools;

/// <summary>Provides generated JSON metadata shared by the runtime and client without reflection fallback. Specified by <c>[DVT-2]</c> and <c>[DVT-14]</c>.</summary>
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
[JsonSerializable(typeof(TimelineEventPayload))]
[JsonSerializable(typeof(TimelineDroppedPayload))]
[JsonSerializable(typeof(StateEditRequestPayload))]
[JsonSerializable(typeof(StateEditResponsePayload))]
internal sealed partial class GeneratedDevToolsJsonSerializerContext : JsonSerializerContext;
