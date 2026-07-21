using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Store;

/// <summary>
/// The per-app store root — the C# port of the <c>Pinia</c> instance produced by <c>createPinia()</c>
/// (<c>packages/pinia/src/createPinia.ts</c>, <c>rootStore.ts</c>). It owns a detached root
/// <see cref="EffectScope"/> (Pinia's <c>pinia._e</c>) and the id → instance map (Pinia's
/// <c>pinia._s</c>): the first resolution of a definition instantiates the store inside a child scope
/// of the root and caches it; later resolutions return the cached instance.
/// <para>
/// Keeping the registry per app instance is what gives server-side (multi-request) hosting its
/// isolation — two registries never share store state, so a long-lived .NET server serves concurrent
/// requests without the global mutable state a fresh JS module graph would provide per request
/// (https://pinia.vuejs.org/ssr/). Install a registry on an <see cref="IApplication"/> with
/// <c>App.Use(registry.AsPlugin())</c>. Not thread-safe (single-threaded JS event-loop
/// model): WASM needs no locks, and servers isolate by using one registry per request rather than a
/// shared singleton.
/// </para>
/// </summary>
public sealed class StoreRegistry : IDisposable
{
    // Pinia's pinia._e: a DETACHED root scope so it never attaches to whatever effect scope happens to
    // be active when the registry is created. Each store's own scope is created as a CHILD of this
    // root (see Resolve), so stopping the root cascades to every store — the app-level disposal path.
    private readonly EffectScope _rootScope = new(detached: true);
    private readonly Dictionary<string, StoreEntry> _stores = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>The injection key the store plugin provides this registry under, app-wide.</summary>
    internal static readonly InjectionKey<StoreRegistry> InjectionKey = new("viu:store-registry");

    /// <summary>Creates an empty registry. Equivalent to <see cref="Stores.CreateRegistry"/>.</summary>
    public StoreRegistry()
    {
    }

    /// <summary>The number of stores instantiated in this registry so far (upstream: <c>pinia._s.size</c>).</summary>
    public int Count => _stores.Count;

    /// <summary>Whether the registry has been disposed (its root scope stopped).</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Wraps this registry as an installable app plugin so it can be applied with
    /// <c>App.Use(registry.AsPlugin())</c> — the C# stand-in for the Pinia <c>pinia</c> object being
    /// itself the plugin passed to <c>app.use(pinia)</c>. Installing provides this registry app-wide
    /// (the final inject fallback) and makes it the ambient <see cref="Stores.ActiveRegistry"/>. The
    /// plugin is platform-neutral (it extends the node-type-agnostic <see cref="IApplication"/>), so
    /// the same plugin installs on a browser app or a server app. Call once per app.
    /// </summary>
    /// <returns>A plugin that installs this registry.</returns>
    public IApplicationPlugin AsPlugin() => new StorePlugin(this);

    /// <summary>
    /// Stops the root effect scope — cascading to every store's child scope, so all computeds and
    /// watchers created in setup stop — clears the store map, and clears the ambient
    /// <see cref="Stores.ActiveRegistry"/> if it points here. The C# realization of disposing the
    /// owning app's store root (Pinia stops <c>pinia._e</c>). Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _rootScope.Stop();
        _stores.Clear();
        if (ReferenceEquals(Stores.ActiveRegistry, this))
        {
            Stores.SetActiveRegistry(null);
        }
    }

    // Resolves (creating on first use) the store for `definition`. Mirrors createSetupStore: the store
    // scope is created while the detached root is the active scope, so it becomes a child of the root
    // and is NOT captured by any component scope active at the call site — the "detached from the
    // render tree" property the ticket calls out. Setup runs once, inside that child scope.
    internal TStore Resolve<TStore>(StoreDefinition<TStore> definition)
        where TStore : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stores.TryGetValue(definition.Id, out var existing))
        {
            // Same id claimed by a different definition is a collision (never silent aliasing); the
            // same definition resolved again is a cache hit.
            if (!ReferenceEquals(existing.Definition, definition))
            {
                throw new DuplicateStoreIdException(definition.Id);
            }
            return (TStore)existing.Instance;
        }
        var scope = _rootScope.Run(static () => new EffectScope(detached: false));
        TStore instance;
        try
        {
            instance = scope.Run(definition.RunSetup);
        }
        catch
        {
            // A throwing setup must not leave a live child scope (or a half-built entry) behind.
            scope.Stop();
            throw;
        }
        _stores[definition.Id] = new StoreEntry(definition, instance, scope);
        return instance;
    }

    // Stops and forgets a single store (Pinia's store.$dispose), but only when `definition` is the one
    // that owns the id, so a stale definition cannot dispose another store. A later resolution rebuilds
    // the store fresh.
    internal void DisposeStore<TStore>(StoreDefinition<TStore> definition)
        where TStore : class
    {
        if (_stores.TryGetValue(definition.Id, out var entry) && ReferenceEquals(entry.Definition, definition))
        {
            entry.Scope.Stop();
            _stores.Remove(definition.Id);
        }
    }
}
