using System;
using System.Collections.Generic;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreRegistry : IStateStoreRegistry
{
    private readonly Dictionary<object, StoreEntry> _entries = new();
    private readonly IServiceProvider? _services;
    private readonly IReactiveWatchScheduler _watchScheduler;

    internal StateStoreRegistry(
        IServiceProvider? services,
        IReactiveWatchScheduler watchScheduler)
    {
        _services = services;
        _watchScheduler = watchScheduler;
    }

    public int Count => _entries.Count;

    public bool IsDisposed { get; private set; }

    public TStore GetOrCreate<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_entries.TryGetValue(definition, out var existing))
        {
            return (TStore)existing.Store;
        }

        var scope = new EffectScope();
        try
        {
            var context = new StateContext(scope, _services, _watchScheduler);
            var store = definition.Activate(context);
            ArgumentNullException.ThrowIfNull(store);
            _entries.Add(definition, new StoreEntry(store, scope));
            return store;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!_entries.Remove(definition, out var entry))
        {
            return false;
        }

        entry.Dispose();
        return true;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (var entry in _entries.Values)
        {
            entry.Dispose();
        }

        _entries.Clear();
    }

    private sealed class StoreEntry : IDisposable
    {
        private readonly EffectScope _scope;

        internal StoreEntry(object store, EffectScope scope)
        {
            Store = store;
            _scope = scope;
        }

        internal object Store { get; }

        public void Dispose()
        {
            _scope.Dispose();
            switch (Store)
            {
                case IAsyncDisposable asynchronousDisposable:
                    asynchronousDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}
