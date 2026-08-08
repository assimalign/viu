using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Owns one lazily materialized state-store set. Each store is created in an attached child of one
/// detached registry scope, so component unmount never ends application state and registry disposal
/// ends every store. Specified by <c>[STA-2]</c> and <c>[STA-3]</c>.
/// </summary>
/// <remarks>
/// This type is not thread-safe. It is designed for Viu's single-threaded host event loop.
/// Its lifetime contract is synchronous: stores implementing <see cref="IDisposable"/> are disposed,
/// while an asynchronous-only store remains responsible for an explicit host-owned lifetime. The
/// registry never blocks the host loop waiting for asynchronous disposal.
/// </remarks>
public sealed class StateStoreRegistry : IStateStoreRegistry
{
    private readonly Dictionary<string, StateStoreEntry> _entries =
        new(StringComparer.Ordinal);
    private readonly IReactiveEffectScopeFactory _effectScopes;
    private readonly IReactiveEffectScope _rootScope;
    private readonly IServiceProvider? _services;
    private readonly IReactiveWatchScheduler? _watchScheduler;

    /// <summary>
    /// Creates a registry with an explicit reactive scope factory. The factory creates one detached
    /// root immediately; store scopes are created lazily as children of that root. Specified by
    /// <c>[STA-2]</c> and <c>[STA-3]</c>.
    /// </summary>
    /// <param name="services">The optional externally owned application service provider.</param>
    /// <param name="effectScopes">The reactive effect-scope factory.</param>
    /// <param name="watchScheduler">
    /// The application watch scheduler, or <see langword="null"/> for synchronous delivery.
    /// </param>
    public StateStoreRegistry(
        IServiceProvider? services,
        IReactiveEffectScopeFactory effectScopes,
        IReactiveWatchScheduler? watchScheduler = null)
    {
        ArgumentNullException.ThrowIfNull(effectScopes);
        _services = services;
        _effectScopes = effectScopes;
        _watchScheduler = watchScheduler;
        _rootScope = effectScopes.Create(isDetached: true);
    }

    /// <inheritdoc />
    public int Count => _entries.Count;

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_entries.TryGetValue(definition.Key, out StateStoreEntry? entry))
        {
            if (!ReferenceEquals(entry.Definition, definition))
            {
                throw new DuplicateStateStoreKeyException(definition.Key);
            }

            if (entry.Instance is TStore existingStore)
            {
                return existingStore;
            }

            throw new InvalidOperationException(
                $"The registry entry for state store \"{definition.Key}\" has an invalid type.");
        }

        IReactiveEffectScope scope =
            _rootScope.Run(() => _effectScopes.Create(isDetached: false));
        try
        {
            StateContext context = new(
                scope,
                _services,
                _watchScheduler);
            IStateContext? previousContext = StateStoreSetupRuntime.Current;
            try
            {
                StateStoreSetupRuntime.Current = context;
                TStore store = scope.Run(() => definition.Setup(context))
                    ?? throw new InvalidOperationException(
                        $"State store setup for \"{definition.Key}\" returned null.");
                _entries.Add(
                    definition.Key,
                    new StateStoreEntry(definition, store, scope));
                return store;
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

    /// <inheritdoc />
    public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!_entries.TryGetValue(definition.Key, out StateStoreEntry? entry)
            || !ReferenceEquals(entry.Definition, definition))
        {
            return false;
        }

        _entries.Remove(definition.Key);
        entry.Dispose();
        return true;
    }

    /// <summary>
    /// Disposes every materialized store, stops every store scope and the detached root, clears the
    /// ambient registry when it references this instance, and rejects future use. Teardown is
    /// idempotent and continues after a cleanup failure before rethrowing the first failure.
    /// Specified by <c>[STA-2]</c>.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        ExceptionDispatchInfo? error = null;
        foreach (StateStoreEntry entry in _entries.Values)
        {
            try
            {
                entry.Dispose();
            }
            catch (Exception exception)
            {
                error ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        try
        {
            _rootScope.Stop();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _entries.Clear();
            if (ReferenceEquals(StateStores.ActiveRegistry, this))
            {
                StateStores.SetActiveRegistry(null);
            }
        }

        error?.Throw();
    }
}
