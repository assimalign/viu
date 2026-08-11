using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.DevTools;

internal sealed class ComponentInspectionRegistry
{
    private readonly ConditionalWeakTable<object, ComponentIdentity> _identities = new();
    private readonly Dictionary<int, WeakReference<object>> _instances = [];
    private readonly Dictionary<int, ComponentEntry> _entries = [];
    private int _nextIdentifier;

    internal ComponentChangePayload Mount(in RuntimeInspectionComponent component)
    {
        ComponentIdentity identity = GetOrCreateIdentity(component.Instance);
        int? parentIdentifier = component.Parent is null
            ? null
            : GetOrCreateIdentity(component.Parent).Identifier;
        UpdateMetadata(identity, in component);

        int index = CountChildren(parentIdentifier);
        ComponentEntry entry = new(
            identity.Identifier,
            parentIdentifier,
            index,
            ResolveName(component),
            component.Key is null ? null : SnapshotValueEncoder.SafeDisplay(component.Key));
        _entries[identity.Identifier] = entry;
        _instances[identity.Identifier] = new WeakReference<object>(component.Instance);
        return entry.ToChangePayload();
    }

    internal ComponentChangePayload Update(in RuntimeInspectionComponent component)
    {
        ComponentIdentity identity = GetOrCreateIdentity(component.Instance);
        int? parentIdentifier = component.Parent is null
            ? null
            : GetOrCreateIdentity(component.Parent).Identifier;
        UpdateMetadata(identity, in component);

        int index = _entries.TryGetValue(identity.Identifier, out ComponentEntry? previous)
            ? previous.Index
            : CountChildren(parentIdentifier);
        ComponentEntry entry = new(
            identity.Identifier,
            parentIdentifier,
            index,
            ResolveName(component),
            component.Key is null ? null : SnapshotValueEncoder.SafeDisplay(component.Key));
        _entries[identity.Identifier] = entry;
        _instances[identity.Identifier] = new WeakReference<object>(component.Instance);
        return entry.ToChangePayload();
    }

    internal int? Unmount(object instance)
    {
        if (!_identities.TryGetValue(instance, out ComponentIdentity? identity)
            || !_entries.ContainsKey(identity.Identifier))
        {
            return null;
        }

        RemoveEntryAndOrphanedDescendants(identity.Identifier);
        return identity.Identifier;
    }

    internal ComponentReorderPayload? Reorder(object instance, int index)
    {
        if (!_identities.TryGetValue(instance, out ComponentIdentity? identity)
            || !_entries.TryGetValue(identity.Identifier, out ComponentEntry? entry)
            || entry.Index == index)
        {
            return null;
        }

        _entries[identity.Identifier] = entry with { Index = index };
        return new ComponentReorderPayload(identity.Identifier, index);
    }

    internal int? FindIdentifier(object instance) =>
        _identities.TryGetValue(instance, out ComponentIdentity? identity)
            && _entries.ContainsKey(identity.Identifier)
                ? identity.Identifier
                : null;

    internal ComponentTreeSnapshotPayload CreateTreeSnapshot()
    {
        List<ComponentTreeNodePayload> roots = [];
        foreach (ComponentEntry entry in _entries.Values)
        {
            if (entry.ParentIdentifier is null
                || !_entries.ContainsKey(entry.ParentIdentifier.Value))
            {
                roots.Add(CreateTreeNode(entry));
            }
        }

        roots.Sort(static (left, right) => left.Identifier.CompareTo(right.Identifier));
        return new ComponentTreeSnapshotPayload(roots);
    }

    internal bool TryCreateSnapshot(
        int identifier,
        int depth,
        SnapshotValueEncoder encoder,
        out ComponentSnapshotResponsePayload response)
    {
        if (!TryGetIdentity(identifier, out object? instance, out ComponentIdentity? identity))
        {
            response = new ComponentSnapshotResponsePayload(
                identifier,
                [],
                [],
                [],
                "The component is no longer mounted.");
            return false;
        }

        IReadOnlyDictionary<string, object?> state = GetState(instance, identity.Metadata);
        response = new ComponentSnapshotResponsePayload(
            identifier,
            encoder.EncodeDictionary(identity.Metadata.Bindings.Parameters, depth),
            encoder.EncodeDictionary(state, depth),
            new List<ComponentEventMetadataPayload>(identity.Metadata.Events),
            null);
        return true;
    }

    internal bool TryExpand(
        int identifier,
        string section,
        IReadOnlyList<string> path,
        int depth,
        SnapshotValueEncoder encoder,
        out DevToolsValuePayload? value,
        out string? error)
    {
        value = null;
        if (!TryGetIdentity(identifier, out object? instance, out ComponentIdentity? identity))
        {
            error = "The component is no longer mounted.";
            return false;
        }

        object root;
        if (string.Equals(section, "parameters", StringComparison.Ordinal))
        {
            root = identity.Metadata.Bindings.Parameters;
        }
        else if (string.Equals(section, "state", StringComparison.Ordinal))
        {
            root = GetState(instance, identity.Metadata);
        }
        else
        {
            error = "The requested snapshot section is unknown.";
            return false;
        }

        if (!SnapshotValueEncoder.TryResolvePath(root, path, out object? resolved))
        {
            error = "The requested expansion path no longer exists.";
            return false;
        }

        value = encoder.EncodeValue(resolved, depth, path);
        error = null;
        return true;
    }

    private ComponentIdentity GetOrCreateIdentity(object instance)
    {
        ComponentIdentity identity = _identities.GetValue(
            instance,
            _ => new ComponentIdentity(checked(++_nextIdentifier)));
        _instances[identity.Identifier] = new WeakReference<object>(instance);
        return identity;
    }

    private bool TryGetIdentity(
        int identifier,
        out object instance,
        out ComponentIdentity identity)
    {
        if (_entries.ContainsKey(identifier)
            && _instances.TryGetValue(identifier, out WeakReference<object>? weakReference)
            && weakReference.TryGetTarget(out instance!)
            && _identities.TryGetValue(instance, out identity!))
        {
            return true;
        }

        _instances.Remove(identifier);
        _entries.Remove(identifier);
        instance = null!;
        identity = null!;
        return false;
    }

    private static void UpdateMetadata(
        ComponentIdentity identity,
        in RuntimeInspectionComponent component)
    {
        List<ComponentEventMetadataPayload> events = new(component.Contract.Events.Count);
        for (int index = 0; index < component.Contract.Events.Count; index++)
        {
            ComponentEvent componentEvent = component.Contract.Events[index];
            events.Add(new ComponentEventMetadataPayload(
                componentEvent.Name,
                componentEvent.Validator is not null));
        }

        identity.Metadata = new ComponentMetadata(
            component.Bindings,
            component.ExposedState,
            events);
    }

    private static IReadOnlyDictionary<string, object?> GetState(
        object instance,
        ComponentMetadata metadata)
    {
        if (instance is IComponentInspectionStateProvider provider)
        {
            try
            {
                return provider.GetInspectionState()
                    ?? new Dictionary<string, object?>
                    {
                        ["state"] = new UnavailableInspectionValue("provider-returned-null"),
                    };
            }
            catch
            {
                return new Dictionary<string, object?>
                {
                    ["state"] = new UnavailableInspectionValue("provider-threw"),
                };
            }
        }

        if (metadata.ExposedState is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        return metadata.ExposedState is null
            ? EmptyState.Values
            : new Dictionary<string, object?>
            {
                ["exposed"] = metadata.ExposedState,
            };
    }

    private ComponentTreeNodePayload CreateTreeNode(ComponentEntry entry)
    {
        List<ComponentEntry> children = [];
        foreach (ComponentEntry candidate in _entries.Values)
        {
            if (candidate.ParentIdentifier == entry.Identifier)
            {
                children.Add(candidate);
            }
        }

        children.Sort(static (left, right) =>
        {
            int position = left.Index.CompareTo(right.Index);
            return position != 0
                ? position
                : left.Identifier.CompareTo(right.Identifier);
        });
        List<ComponentTreeNodePayload> childPayloads = new(children.Count);
        for (int index = 0; index < children.Count; index++)
        {
            childPayloads.Add(CreateTreeNode(children[index]));
        }

        return new ComponentTreeNodePayload(
            entry.Identifier,
            entry.Name,
            entry.Key,
            childPayloads);
    }

    private int CountChildren(int? parentIdentifier)
    {
        int count = 0;
        foreach (ComponentEntry entry in _entries.Values)
        {
            if (entry.ParentIdentifier == parentIdentifier)
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveEntryAndOrphanedDescendants(int identifier)
    {
        List<int> descendants = [];
        foreach (ComponentEntry entry in _entries.Values)
        {
            if (entry.ParentIdentifier == identifier)
            {
                descendants.Add(entry.Identifier);
            }
        }

        for (int index = 0; index < descendants.Count; index++)
        {
            RemoveEntryAndOrphanedDescendants(descendants[index]);
        }

        _entries.Remove(identifier);
        _instances.Remove(identifier);
    }

    private static string ResolveName(in RuntimeInspectionComponent component) =>
        component.Contract.DisplayName
        ?? component.Instance.GetType().Name;

    private sealed class ComponentIdentity
    {
        internal ComponentIdentity(int identifier)
        {
            Identifier = identifier;
            Metadata = new ComponentMetadata(
                new ComponentBindings(),
                null,
                []);
        }

        internal int Identifier { get; }

        internal ComponentMetadata Metadata { get; set; }
    }

    private sealed record ComponentMetadata(
        ComponentBindings Bindings,
        object? ExposedState,
        List<ComponentEventMetadataPayload> Events);

    private sealed record ComponentEntry(
        int Identifier,
        int? ParentIdentifier,
        int Index,
        string Name,
        string? Key)
    {
        internal ComponentChangePayload ToChangePayload() => new(
            Identifier,
            ParentIdentifier,
            Index,
            Name,
            Key);
    }

    private static class EmptyState
    {
        internal static IReadOnlyDictionary<string, object?> Values { get; } =
            new Dictionary<string, object?>();
    }
}
