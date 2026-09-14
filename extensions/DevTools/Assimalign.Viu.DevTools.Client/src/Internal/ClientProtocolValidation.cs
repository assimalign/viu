using System.Collections.Generic;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client;

// [DVT-14]: nullable annotations describe trusted records, not untrusted incoming JSON.
// Validate entire nested payloads before publishing them to stores or compiled panes.
internal static class ClientProtocolValidation
{
    internal static bool IsValid(object? value, int depth = 0)
    {
        if (depth > 64)
        {
            return false;
        }

        return value switch
        {
            HandshakeResponsePayload response => response.Accepted
                ? response.Version.HasValue : response.Reason is not null,
            ComponentChangePayload change => change.Identifier > 0 && change.Index >= 0
                && HasName(change.Name) && (!change.ParentIdentifier.HasValue
                    || change.ParentIdentifier > 0 && change.ParentIdentifier != change.Identifier),
            ComponentIdentifierPayload identifier => identifier.Identifier > 0,
            ComponentReorderPayload reorder => reorder.Identifier > 0 && reorder.Index >= 0,
            ComponentTreeSnapshotPayload tree => All(tree.Roots, depth)
                && UniqueComponents(tree.Roots, new HashSet<int>()),
            ComponentTreeNodePayload node => node.Identifier > 0 && HasName(node.Name)
                && All(node.Children, depth),
            ComponentSnapshotResponsePayload snapshot => snapshot.Identifier > 0
                && All(snapshot.Parameters, depth) && All(snapshot.State, depth) && All(snapshot.Events, depth),
            ComponentEventMetadataPayload componentEvent => HasName(componentEvent.Name),
            ComponentExpansionResponsePayload expansion => expansion.Identifier > 0
                && expansion.Section is "parameters" or "state" && Path(expansion.Path)
                && (expansion.Value is null || IsValid(expansion.Value, depth + 1)),
            StateEditResponsePayload edit => edit.Identifier > 0 && Path(edit.Path),
            DevToolsNamedValuePayload named => named.Name is not null && IsValid(named.Value, depth + 1),
            DevToolsValuePayload state => HasName(state.Kind) && HasName(state.TypeName)
                && Path(state.Path) && All(state.Children, depth),
            TimelineDroppedPayload dropped => dropped.Count >= 0,
            TimelineLayerPayload layer => HasName(layer.Identifier) && HasName(layer.DisplayName),
            InspectorRegistrationPayload inspector => HasName(inspector.Identifier) && HasName(inspector.DisplayName),
            InspectorIdentifierPayload inspector => HasName(inspector.Identifier),
            InspectorTreeResponsePayload tree => HasName(tree.InspectorIdentifier) && All(tree.Roots, depth),
            InspectorNodePayload node => HasName(node.Identifier) && node.Label is not null && All(node.Children, depth),
            InspectorStateResponsePayload state => HasName(state.InspectorIdentifier)
                && HasName(state.NodeIdentifier) && All(state.State, depth),
            _ => false,
        };
    }

    internal static bool IsValid(TimelineEventPayload observation) =>
        observation.Sequence >= 0 && observation.Timestamp >= 0 && observation.CorrelationIdentifier >= 0
        && HasName(observation.LayerIdentifier) && HasName(observation.Kind) && observation.Label is not null;

    private static bool HasName(string? name) => !string.IsNullOrEmpty(name);

    private static bool Path(IReadOnlyList<string>? path)
    {
        if (path is null || path.Count > 64)
        {
            return false;
        }

        foreach (string segment in path)
        {
            if (segment is null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool All<T>(IReadOnlyList<T>? values, int depth)
    {
        if (values is null)
        {
            return false;
        }

        foreach (T value in values)
        {
            if (!IsValid(value, depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool UniqueComponents(IReadOnlyList<ComponentTreeNodePayload> nodes, HashSet<int> identifiers)
    {
        foreach (ComponentTreeNodePayload node in nodes)
        {
            if (!identifiers.Add(node.Identifier) || !UniqueComponents(node.Children, identifiers))
            {
                return false;
            }
        }

        return true;
    }
}
