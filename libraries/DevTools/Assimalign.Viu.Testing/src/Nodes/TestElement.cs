using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Represents an in-memory element and its host bindings, listeners, and children.</summary>
/// <remarks>Specified by <c>[RND-HOST-1]</c>, <c>[RND-HOST-3]</c>, and <c>[CONF-3]</c>.</remarks>
[DebuggerDisplay("<{Name,nq}> #{Identifier} Properties = {_properties.Count}, Children = {_children.Count}, EventListeners = {_eventListeners.Count}")]
public sealed class TestElement : TestNode
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.Ordinal);
    private readonly List<TestNode> _children = [];
    private readonly Dictionary<string, Delegate> _eventListeners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TestEventListener> _eventListenerRegistrations =
        new(StringComparer.Ordinal);

    internal TestElement(QualifiedName name)
    {
        Name = name;
        Properties = new ReadOnlyDictionary<string, object?>(_properties);
        Children = _children.AsReadOnly();
        EventListeners = new ReadOnlyDictionary<string, Delegate>(_eventListeners);
    }

    /// <summary>Gets the complete qualified element name supplied by the renderer.</summary>
    public QualifiedName Name { get; }

    /// <summary>Gets the local element name.</summary>
    public string Tag => Name.LocalName;

    /// <summary>Gets the optional namespace name.</summary>
    public string? Namespace => Name.NamespaceName;

    /// <summary>
    /// Gets a read-only live view of the host attributes and properties as last patched.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }

    /// <summary>Gets a read-only live view of child nodes in host order.</summary>
    public IReadOnlyList<TestNode> Children { get; }

    /// <summary>
    /// Gets a read-only live view of event listeners keyed by normalized event name.
    /// </summary>
    public IReadOnlyDictionary<string, Delegate> EventListeners { get; }

    internal int IndexOfChild(TestNode child) => _children.IndexOf(child);

    internal void InsertChild(int index, TestNode child) => _children.Insert(index, child);

    internal void AddChild(TestNode child) => _children.Add(child);

    internal void RemoveChild(TestNode child) => _children.Remove(child);

    internal void SetProperty(string name, object? value) => _properties[name] = value;

    internal void RemoveProperty(string name) => _properties.Remove(name);

    internal void SetEventListener(TestEventName name, Delegate listener)
    {
        _eventListeners[name.EventName] = listener;
        _eventListenerRegistrations[name.EventName] = new TestEventListener(listener, name.Once);
    }

    internal void RemoveEventListener(string name)
    {
        string eventName = TestEventName.Parse(name).EventName;
        _eventListeners.Remove(eventName);
        _eventListenerRegistrations.Remove(eventName);
    }

    internal bool TryTakeEventListener(string name, out Delegate? listener)
    {
        string eventName = TestEventName.Parse(name).EventName;
        if (!_eventListenerRegistrations.TryGetValue(
                eventName,
                out TestEventListener? registration))
        {
            listener = null;
            return false;
        }

        listener = registration.Listener;
        if (registration.Once)
        {
            _eventListeners.Remove(eventName);
            _eventListenerRegistrations.Remove(eventName);
        }

        return true;
    }
}
