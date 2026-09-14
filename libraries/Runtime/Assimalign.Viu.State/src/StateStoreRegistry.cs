using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text.Json;

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
public sealed class StateStoreRegistry : IStateStoreRegistry, IStateStorePayloadRegistry
{
    private readonly Dictionary<string, StateStoreEntry> _entries =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _creatingDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StateStoreEntry> _creatingEntries = new(StringComparer.Ordinal);
    private readonly List<IStateStorePlugin> _plugins = new();
    private readonly IReactiveEffectScopeFactory _effectScopes;
    private readonly IReactiveEffectScope _rootScope;
    private readonly IServiceProvider? _services;
    private readonly IReactiveWatchScheduler? _watchScheduler;
    private StateStorePayload? _restorePayload;

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
    public IStateStoreRegistry Use(IStateStorePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _plugins.Add(plugin);
        return this;
    }

    /// <inheritdoc />
    public TExtension GetExtension<TExtension>(object store)
        where TExtension : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        foreach (StateStoreEntry entry in _entries.Values)
        {
            if (ReferenceEquals(entry.Instance, store))
            {
                return entry.GetExtension<TExtension>();
            }
        }

        foreach (StateStoreEntry entry in _creatingEntries.Values)
        {
            if (ReferenceEquals(entry.Instance, store))
            {
                return entry.GetExtension<TExtension>();
            }
        }

        throw new InvalidOperationException("The supplied store is not owned by this registry.");
    }

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

        if (_creatingDefinitions.TryGetValue(definition.Key, out object? creatingDefinition))
        {
            if (!ReferenceEquals(creatingDefinition, definition))
            {
                throw new DuplicateStateStoreKeyException(definition.Key);
            }

            throw new InvalidOperationException(
                $"State store \"{definition.Key}\" cannot recursively resolve itself during creation.");
        }

        IStateStorePlugin[] plugins = _plugins.ToArray();
        _creatingDefinitions.Add(definition.Key, definition);
        IReactiveEffectScope? scope = null;
        StateStoreEntry? createdEntry = null;
        try
        {
            scope = _rootScope.Run(() => _effectScopes.Create(isDetached: false));
            StateContext context = new(
                scope,
                _services,
                _watchScheduler)
            {
                IsInitializing = definition.Persistence is not null,
            };
            IStateContext? previousContext = StateStoreSetupRuntime.Current;
            try
            {
                StateStoreSetupRuntime.Current = context;
                TStore store = scope.Run(() => definition.Setup(context))
                    ?? throw new InvalidOperationException(
                        $"State store setup for \"{definition.Key}\" returned null.");
                IStateStoreSerializer<TStore>? serializer = definition.Serializer;
                createdEntry = new StateStoreEntry(
                    definition.Key,
                    definition,
                    store,
                    scope,
                    serializer is null
                        ? null
                        : writer => serializer.Serialize(writer, store),
                    serializer is null
                        ? null
                        : state => serializer.Restore(store, state));
                EnsureCreationActive(scope);
                _creatingEntries.Add(definition.Key, createdEntry);
                StateStorePluginContext pluginContext = new(
                    this,
                    createdEntry,
                    context,
                    definition.Identifier,
                    definition.Persistence);
                foreach (IStateStorePlugin plugin in plugins)
                {
                    scope.Run(() => plugin.Apply(pluginContext));
                    EnsureCreationActive(scope);
                }

                if (_restorePayload is { } restorePayload
                    && restorePayload.TryGetState(
                        definition.Key,
                        out JsonElement state))
                {
                    createdEntry.RestoreState(state);
                }

                EnsureCreationActive(scope);
                scope.Run(context.CompleteInitialization);
                EnsureCreationActive(scope);
                _entries.Add(definition.Key, createdEntry);
                return store;
            }
            finally
            {
                StateStoreSetupRuntime.Current = previousContext;
            }
        }
        catch (Exception exception)
        {
            ExceptionDispatchInfo failure = ExceptionDispatchInfo.Capture(exception);
            try
            {
                if (createdEntry is null)
                {
                    scope?.Stop();
                }
                else
                {
                    createdEntry.Dispose();
                }
            }
            catch
            {
                // Preserve the setup or restore failure after completing best-effort teardown.
            }

            failure.Throw();
            throw;
        }
        finally
        {
            _creatingEntries.Remove(definition.Key);
            _creatingDefinitions.Remove(definition.Key);
        }
    }

    private void EnsureCreationActive(IReactiveEffectScope scope)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!scope.IsActive)
        {
            throw new InvalidOperationException("A state store scope was stopped during creation.");
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

    /// <inheritdoc />
    public StateStorePayload CapturePayload()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return StateStorePayload.Create(_entries);
    }

    /// <inheritdoc />
    public void RestorePayload(StateStorePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        foreach (KeyValuePair<string, StateStoreEntry> entry in _entries)
        {
            if (payload.TryGetState(entry.Key, out JsonElement state))
            {
                entry.Value.RestoreState(state);
            }
        }

        _restorePayload = payload;
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
            _plugins.Clear();
            if (ReferenceEquals(StateStores.ActiveRegistry, this))
            {
                StateStores.SetActiveRegistry(null);
            }
        }

        error?.Throw();
    }
}
