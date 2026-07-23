using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Store;

/// <summary>
/// A typed store definition — the C# port of the <c>useStore</c> function returned by Pinia's
/// <c>defineStore(id, setup)</c> (https://pinia.vuejs.org/core-concepts/#setup-stores,
/// <c>packages/pinia/src/store.ts</c>). It couples the store's <see cref="Id"/> with its setup
/// delegate and resolves the store instance against a <see cref="StoreRegistry"/>: the first
/// resolution per registry runs setup once inside the store's own effect scope; later resolutions
/// return the identical instance.
/// <para>
/// Create definitions with <see cref="Stores.DefineStore{TStore}(string, StoreSetup{TStore})"/> and
/// share the returned instance (declare it <c>static readonly</c>). Not thread-safe (single-threaded
/// JS event-loop model).
/// </para>
/// </summary>
/// <typeparam name="TStore">The store instance type the setup produces.</typeparam>
public sealed class StoreDefinition<TStore>
    where TStore : class
{
    private readonly StoreSetup<TStore> _setup;

    internal StoreDefinition(string id, StoreSetup<TStore> setup)
    {
        Id = id;
        _setup = setup;
    }

    /// <summary>The store's unique id within a registry (upstream: the first argument to <c>defineStore</c>).</summary>
    public string Id { get; }

    /// <summary>
    /// Resolves this store from <paramref name="registry"/> — the DI-friendly form of Pinia's
    /// <c>useStore(pinia)</c> that needs no component tree. The first call per registry runs setup
    /// once and caches the instance inside its own scope; every later call returns that same instance
    /// (reference equality).
    /// </summary>
    /// <param name="registry">The registry that owns the store instance.</param>
    /// <returns>The store instance for <paramref name="registry"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="DuplicateStoreIdException">A different definition already owns <see cref="Id"/> in <paramref name="registry"/>.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="registry"/> has been disposed.</exception>
    public TStore UseStore(StoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Resolve(this);
    }

    /// <summary>
    /// Resolves this store from the ambient registry — the C# port of Pinia's argument-less
    /// <c>useStore()</c>. Inside a component <c>Setup</c> the registry is injected from the current
    /// app context (the one installed with <c>App.Use</c>); outside a component it falls back to
    /// <see cref="Stores.ActiveRegistry"/>. Prefer <see cref="UseStore(StoreRegistry)"/> on the
    /// server, where the ambient registry is not request-isolated.
    /// </summary>
    /// <returns>The store instance for the ambient registry.</returns>
    /// <exception cref="InvalidOperationException">No registry is available from the component context or as the active registry.</exception>
    /// <exception cref="DuplicateStoreIdException">A different definition already owns <see cref="Id"/> in the resolved registry.</exception>
    public TStore UseStore()
    {
        var registry = ResolveAmbientRegistry()
            ?? throw new InvalidOperationException(
                $"No store registry is available to resolve store \"{Id}\". Install one on the app with "
                + "App.Use(registry.AsPlugin()), pass a registry to UseStore(registry), or set one "
                + "with Stores.SetActiveRegistry(...).");
        return registry.Resolve(this);
    }

    /// <summary>
    /// Disposes this store within <paramref name="registry"/> — the C# port of Pinia's
    /// <c>store.$dispose()</c> (<c>packages/pinia/src/store.ts</c>). Stops the store's effect scope
    /// (so its setup computeds and watchers stop firing) and forgets it, leaving the registry's other
    /// stores untouched. A later <see cref="UseStore(StoreRegistry)"/> rebuilds a fresh instance.
    /// No-op when the store was never resolved in <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">The registry holding the store instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public void Dispose(StoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.DisposeStore(this);
    }

    internal TStore RunSetup() => _setup();

    private static StoreRegistry? ResolveAmbientRegistry()
    {
        // Inside a component Setup, resolve service-first-then-provide ([V01.01.03.24]):
        if (ComponentInstance.Current is not null)
        {
            // 1. The application service provider — a registry registered via builder.AddStore /
            //    Services.AddSingleton(registry). Absent on a provide-only app, so this falls through
            //    and the existing provide-based path (and its tests) is unchanged.
            var fromServices = ComponentInstance.Current.Services?.GetService<StoreRegistry>();
            if (fromServices is not null)
            {
                return fromServices;
            }
            // 2. The registry provided app-wide by the store plugin (upstream: useStore injects
            //    piniaSymbol from the current component). The defaulted Inject overload treats a missing
            //    provide as a silent null rather than a dev "injection not found" warning; the
            //    (StoreRegistry)null! default keeps the key's non-nullable type argument (avoiding an
            //    InjectionKey<StoreRegistry?> variance mismatch) and selects the value overload unambiguously.
            StoreRegistry? injected = DependencyInjection.Inject(StoreRegistry.InjectionKey, (StoreRegistry)null!);
            if (injected is not null)
            {
                return injected;
            }
        }
        // Outside a component (plain C#/DI): the ambient active registry (upstream: activePinia).
        return Stores.ActiveRegistry;
    }
}
