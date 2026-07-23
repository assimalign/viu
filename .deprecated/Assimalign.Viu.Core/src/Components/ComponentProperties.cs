using System;
using System.Collections.Generic;


namespace Assimalign.Viu;

/// <summary>
/// The instance's shallow-reactive props object — the C# port of upstream's
/// <c>shallowReactive</c> props (<c>packages/runtime-core/src/componentProps.ts</c>,
/// https://vuejs.org/guide/components/props.html). Reads inside an effect track per prop name
/// (one <see cref="Dependency"/> per read name), and a parent patch that actually changes a
/// value triggers exactly the effects that read it — a child re-renders only when a prop it
/// used changed. Writes from the child produce the one-way-data-flow dev warning and are
/// ignored (upstream parity). Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ComponentProperties
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dependency> _dependencies = new(StringComparer.Ordinal);
    private readonly string _componentName;

    internal ComponentProperties(string componentName)
    {
        _componentName = componentName;
    }

    /// <summary>Reads a prop, tracking it in the active effect (shallow: the value itself is not wrapped).</summary>
    /// <param name="name">The camelCase prop name.</param>
    public object? this[string name]
    {
        get
        {
            GetDependency(name).Track();
            _values.TryGetValue(name, out var value);
            return value;
        }
    }

    /// <summary>Reads a prop as <typeparamref name="T"/>, tracking it in the active effect.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="name">The camelCase prop name.</param>
    /// <returns>The value, or <c>default</c> when absent or of another type.</returns>
    public T? Get<T>(string name) => this[name] is T typed ? typed : default;

    /// <summary>Whether the prop currently has a value (tracked).</summary>
    /// <param name="name">The camelCase prop name.</param>
    public bool Contains(string name)
    {
        GetDependency(name).Track();
        return _values.ContainsKey(name);
    }

    /// <summary>
    /// Rejected with a dev warning: props are one-way data flow — the parent owns them
    /// (upstream parity: the dev-mode props proxy blocks writes).
    /// </summary>
    /// <param name="name">The prop name.</param>
    /// <param name="value">Ignored.</param>
    public void Set(string name, object? value)
        => RuntimeWarnings.Warn(
            $"Attempting to mutate prop \"{name}\" on component <{_componentName}>. Props are readonly — "
            + "use an emit to ask the owner to change it.");

    internal void SetFromOwner(string name, object? value)
    {
        if (_values.TryGetValue(name, out var existing) && Equals(existing, value))
        {
            return;
        }
        _values[name] = value;
        if (_dependencies.TryGetValue(name, out var dependency))
        {
            dependency.Trigger();
        }
    }

    internal void RemoveFromOwner(string name)
    {
        if (_values.Remove(name) && _dependencies.TryGetValue(name, out var dependency))
        {
            dependency.Trigger();
        }
    }

    internal IReadOnlyDictionary<string, object?> Snapshot => _values;

    private Dependency GetDependency(string name)
    {
        if (!_dependencies.TryGetValue(name, out var dependency))
        {
            dependency = new Dependency();
            _dependencies[name] = dependency;
        }
        return dependency;
    }
}
