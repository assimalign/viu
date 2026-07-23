using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// The default per-application state store registry. Each store is created exactly once in a
/// detached reactive scope and is stopped with the registry.
/// </summary>
/// <remarks>Not thread-safe; designed for the browser's single-threaded event loop.</remarks>
public sealed class StateStoreRegistry : IStateStoreRegistry
{
    private readonly IComponentFactory _components;
    private readonly IReactiveScopeFactory _scopes;
    private readonly Dictionary<string, StateStoreEntry> _stores = new(StringComparer.Ordinal);

    /// <summary>Creates a state store registry.</summary>
    /// <param name="components">The shared component activator and dependency resolver.</param>
    /// <param name="scopes">The reactive scope factory.</param>
    public StateStoreRegistry(IComponentFactory components, IReactiveScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(scopes);
        _components = components;
        _scopes = scopes;
    }

    /// <inheritdoc/>
    public int Count => _stores.Count;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public TStore GetOrCreate<TStore>(
        StateStoreDefinition<TStore> definition,
        IComponentContext? owner = null)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_stores.TryGetValue(definition.Key, out StateStoreEntry? entry))
        {
            if (!ReferenceEquals(entry.Definition, definition))
            {
                throw new InvalidOperationException(
                    $"State store key \"{definition.Key}\" is already owned by a different definition.");
            }

            return (TStore)entry.Store;
        }

        IReactiveScope scope = _scopes.Create(isDetached: true);
        try
        {
            StateContext context = new(scope, _components, owner);
            TStore store = scope.Run(() => definition.Setup(context))
                ?? throw new InvalidOperationException(
                    $"State store setup for \"{definition.Key}\" returned null.");
            _stores.Add(definition.Key, new StateStoreEntry(definition, store, scope));
            return store;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!_stores.TryGetValue(definition.Key, out StateStoreEntry? entry)
            || !ReferenceEquals(entry.Definition, definition))
        {
            return false;
        }

        _stores.Remove(definition.Key);
        entry.Scope.Dispose();
        return true;
    }

    /// <summary>Stops every initialized store scope. Idempotent.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (StateStoreEntry entry in _stores.Values)
        {
            entry.Scope.Dispose();
        }

        _stores.Clear();
    }
}

