using System.Collections.Generic;
using System.Text.Json;

namespace Assimalign.Viu.DevTools;

internal sealed record ProtocolBatch(
    string Protocol,
    List<ProtocolEnvelope> Messages);

internal sealed record ProtocolEnvelope(
    int Version,
    string Type,
    JsonElement Payload);

internal sealed record HandshakeRequestPayload(List<int> SupportedVersions);

internal sealed record HandshakeResponsePayload(
    bool Accepted,
    int? Version,
    string? Reason);

internal sealed record EmptyPayload;

internal sealed record TelemetryDroppedPayload(int Count);

internal sealed record ComponentChangePayload(
    int Identifier,
    int? ParentIdentifier,
    int Index,
    string Name,
    string? Key);

internal sealed record ComponentIdentifierPayload(int Identifier);

internal sealed record ComponentReorderPayload(int Identifier, int Index);

internal sealed record ComponentEventPayload(
    int Identifier,
    string Name,
    List<DevToolsValuePayload> Arguments);

internal sealed record ComponentSnapshotRequestPayload(int Identifier, int Depth);

internal sealed record ComponentSnapshotResponsePayload(
    int Identifier,
    List<DevToolsNamedValuePayload> Parameters,
    List<DevToolsNamedValuePayload> State,
    List<ComponentEventMetadataPayload> Events,
    string? Error);

internal sealed record ComponentExpansionRequestPayload(
    int Identifier,
    string Section,
    List<string> Path,
    int Depth);

internal sealed record ComponentExpansionResponsePayload(
    int Identifier,
    string Section,
    List<string> Path,
    DevToolsValuePayload? Value,
    string? Error);

internal sealed record ComponentEventMetadataPayload(
    string Name,
    bool HasValidator);

internal sealed record ComponentTreeSnapshotPayload(
    List<ComponentTreeNodePayload> Roots);

internal sealed record ComponentTreeNodePayload(
    int Identifier,
    string Name,
    string? Key,
    List<ComponentTreeNodePayload> Children);

internal sealed record DevToolsNamedValuePayload(
    string Name,
    DevToolsValuePayload Value);

internal sealed record DevToolsValuePayload(
    string Kind,
    string TypeName,
    string? DisplayValue,
    bool Expandable,
    List<string> Path,
    List<DevToolsNamedValuePayload> Children);

internal sealed record InspectorRegistrationPayload(
    string Identifier,
    string DisplayName);

internal sealed record InspectorIdentifierPayload(string Identifier);

internal sealed record InspectorTreeRequestPayload(string InspectorIdentifier);

internal sealed record InspectorTreeResponsePayload(
    string InspectorIdentifier,
    List<InspectorNodePayload> Roots,
    string? Error);

internal sealed record InspectorNodePayload(
    string Identifier,
    string Label,
    List<InspectorNodePayload> Children);

internal sealed record InspectorStateRequestPayload(
    string InspectorIdentifier,
    string NodeIdentifier,
    int Depth);

internal sealed record InspectorStateResponsePayload(
    string InspectorIdentifier,
    string NodeIdentifier,
    List<DevToolsNamedValuePayload> State,
    string? Error);

internal sealed record TimelineLayerPayload(
    string Identifier,
    string DisplayName,
    string? Color);
