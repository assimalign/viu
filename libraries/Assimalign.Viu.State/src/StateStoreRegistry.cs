using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// The default per-application state store registry. Each store is created exactly once in a
/// child scope of one detached registry root and is stopped with the registry.
/// </summary>
/// <remarks>Not thread-safe; designed for the browser's single-threaded event loop.</remarks>
public sealed class StateStoreRegistry : IStateStoreRegistry
{
    private readonly IComponentFactory _components;
    private readonly IServiceProvider _services;
    private readonly IReactiveEffectScopeFactory _scopes;
    private readonly IReactiveEffectScope _rootScope;
    private readonly IReactiveWatchScheduler? _watchScheduler;
    private readonly Dictionary<string, StateStoreEntry> _stores = new(StringComparer.Ordinal);

    /// <summary>Creates a state store registry.</summary>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The independently supplied application service resolver.</param>
    /// <param name="scopes">The reactive scope factory.</param>
    /// <param name="watchScheduler">
    /// The application watch scheduler, or null to use standalone Reactivity's synchronous watch
    /// behavior.
    /// </param>
    public StateStoreRegistry(
        IComponentFactory components,
        IServiceProvider services,
        IReactiveEffectScopeFactory scopes,
        IReactiveWatchScheduler? watchScheduler = null)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(scopes);
        _components = components;
        _services = services;
        _scopes = scopes;
        _watchScheduler = watchScheduler;
        _rootScope = scopes.Create(isDetached: true);
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
                throw new DuplicateStateStoreKeyException(definition.Key);
            }

            return (TStore)entry.Instance;
        }

        IReactiveEffectScope scope =
            _rootScope.Run(() => _scopes.Create(isDetached: false));
        try
        {
            StateContext context = new(
                scope,
                _components,
                _services,
                _watchScheduler,
                owner);
            IStateContext? previousContext = StateStoreSetupRuntime.Current;
            try
            {
                StateStoreSetupRuntime.Current = context;
                TStore instance = scope.Run(() => definition.Setup(context))
                    ?? throw new InvalidOperationException(
                        $"State store setup for \"{definition.Key}\" returned null.");
                _stores.Add(
                    definition.Key,
                    new StateStoreEntry(definition, instance, scope));
                return instance;
            }
            finally
            {
                StateStoreSetupRuntime.Current = previousContext;
            }
        }
        catch
        {
            scope.Stop();
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
        entry.Scope.Stop();
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
        try
        {
            _rootScope.Stop();
        }
        finally
        {
            _stores.Clear();
            if (ReferenceEquals(StateStores.ActiveRegistry, this))
            {
                StateStores.SetActiveRegistry(null);
            }
        }
    }
}
