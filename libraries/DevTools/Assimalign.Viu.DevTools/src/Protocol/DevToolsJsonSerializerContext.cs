using System.Text.Json.Serialization.Metadata;

namespace Assimalign.Viu.DevTools;

/// <summary>Exposes the shared source-generated protocol metadata to runtime and client without reflection fallback.</summary>
/// <remarks>The generated context remains internal because its emitted members have oblivious nullability. This facade publishes explicit nullable-aware metadata contracts. Specified by <c>[DVT-2]</c> and <c>[DVT-14]</c>.</remarks>
public sealed class DevToolsJsonSerializerContext
{
    private DevToolsJsonSerializerContext() { }

    /// <summary>Gets the shared generated protocol metadata facade.</summary>
    public static DevToolsJsonSerializerContext Default { get; } = new();

    /// <summary>Gets generated metadata for <see cref="ProtocolBatch"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ProtocolBatch> ProtocolBatch =>
        GeneratedDevToolsJsonSerializerContext.Default.ProtocolBatch;

    /// <summary>Gets generated metadata for <see cref="ProtocolEnvelope"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ProtocolEnvelope> ProtocolEnvelope =>
        GeneratedDevToolsJsonSerializerContext.Default.ProtocolEnvelope;

    /// <summary>Gets generated metadata for <see cref="HandshakeRequestPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<HandshakeRequestPayload> HandshakeRequestPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.HandshakeRequestPayload;

    /// <summary>Gets generated metadata for <see cref="HandshakeResponsePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<HandshakeResponsePayload> HandshakeResponsePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.HandshakeResponsePayload;

    /// <summary>Gets generated metadata for <see cref="EmptyPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<EmptyPayload> EmptyPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.EmptyPayload;

    /// <summary>Gets generated metadata for <see cref="TelemetryDroppedPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<TelemetryDroppedPayload> TelemetryDroppedPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.TelemetryDroppedPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentChangePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentChangePayload> ComponentChangePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentChangePayload;

    /// <summary>Gets generated metadata for <see cref="ComponentIdentifierPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentIdentifierPayload> ComponentIdentifierPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentIdentifierPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentReorderPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentReorderPayload> ComponentReorderPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentReorderPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentEventPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentEventPayload> ComponentEventPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentEventPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentSnapshotRequestPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentSnapshotRequestPayload> ComponentSnapshotRequestPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentSnapshotRequestPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentSnapshotResponsePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentSnapshotResponsePayload> ComponentSnapshotResponsePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentSnapshotResponsePayload;

    /// <summary>Gets generated metadata for <see cref="ComponentExpansionRequestPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentExpansionRequestPayload> ComponentExpansionRequestPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentExpansionRequestPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentExpansionResponsePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentExpansionResponsePayload> ComponentExpansionResponsePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentExpansionResponsePayload;

    /// <summary>Gets generated metadata for <see cref="ComponentEventMetadataPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentEventMetadataPayload> ComponentEventMetadataPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentEventMetadataPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentTreeSnapshotPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentTreeSnapshotPayload> ComponentTreeSnapshotPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentTreeSnapshotPayload;

    /// <summary>Gets generated metadata for <see cref="ComponentTreeNodePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<ComponentTreeNodePayload> ComponentTreeNodePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.ComponentTreeNodePayload;

    /// <summary>Gets generated metadata for <see cref="DevToolsNamedValuePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<DevToolsNamedValuePayload> DevToolsNamedValuePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.DevToolsNamedValuePayload;

    /// <summary>Gets generated metadata for <see cref="DevToolsValuePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<DevToolsValuePayload> DevToolsValuePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.DevToolsValuePayload;

    /// <summary>Gets generated metadata for <see cref="InspectorRegistrationPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorRegistrationPayload> InspectorRegistrationPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorRegistrationPayload;

    /// <summary>Gets generated metadata for <see cref="InspectorIdentifierPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorIdentifierPayload> InspectorIdentifierPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorIdentifierPayload;

    /// <summary>Gets generated metadata for <see cref="InspectorTreeRequestPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorTreeRequestPayload> InspectorTreeRequestPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorTreeRequestPayload;

    /// <summary>Gets generated metadata for <see cref="InspectorTreeResponsePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorTreeResponsePayload> InspectorTreeResponsePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorTreeResponsePayload;

    /// <summary>Gets generated metadata for <see cref="InspectorNodePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorNodePayload> InspectorNodePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorNodePayload;

    /// <summary>Gets generated metadata for <see cref="InspectorStateRequestPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorStateRequestPayload> InspectorStateRequestPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorStateRequestPayload;

    /// <summary>Gets generated metadata for <see cref="InspectorStateResponsePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<InspectorStateResponsePayload> InspectorStateResponsePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.InspectorStateResponsePayload;

    /// <summary>Gets generated metadata for <see cref="TimelineLayerPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<TimelineLayerPayload> TimelineLayerPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.TimelineLayerPayload;

    /// <summary>Gets generated metadata for <see cref="TimelineEventPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<TimelineEventPayload> TimelineEventPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.TimelineEventPayload;

    /// <summary>Gets generated metadata for <see cref="TimelineDroppedPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<TimelineDroppedPayload> TimelineDroppedPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.TimelineDroppedPayload;

    /// <summary>Gets generated metadata for <see cref="StateEditRequestPayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<StateEditRequestPayload> StateEditRequestPayload =>
        GeneratedDevToolsJsonSerializerContext.Default.StateEditRequestPayload;

    /// <summary>Gets generated metadata for <see cref="StateEditResponsePayload"/>; runtime contract discovery is never used.</summary>
    public JsonTypeInfo<StateEditResponsePayload> StateEditResponsePayload =>
        GeneratedDevToolsJsonSerializerContext.Default.StateEditResponsePayload;
}
