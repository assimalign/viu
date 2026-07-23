using System;

namespace Assimalign.Viu.Store;

/// <summary>
/// The entry-point facade for Viu's store system — the C# port of the module-level surface of Pinia
/// (https://pinia.vuejs.org, github.com/vuejs/pinia): <see cref="DefineStore{TStore}"/> mirrors
/// <c>defineStore()</c>, <see cref="CreateRegistry"/> mirrors <c>createPinia()</c>, and
/// <see cref="ActiveRegistry"/>/<see cref="SetActiveRegistry"/> mirror <c>activePinia</c>/
/// <c>setActivePinia()</c> (<c>packages/pinia/src/rootStore.ts</c>).
/// <para>
/// <see cref="ActiveRegistry"/> is ambient <c>static</c> state and is NOT thread-safe by design (the
/// single-threaded JS event-loop model). On a multi-request server do not rely on it: create one
/// <see cref="StoreRegistry"/> per request and resolve stores through
/// <see cref="StoreDefinition{TStore}.UseStore(StoreRegistry)"/> so requests stay isolated
/// (https://pinia.vuejs.org/ssr/).
/// </para>
/// </summary>
public static class Stores
{
    private static StoreRegistry? _activeRegistry;

    /// <summary>
    /// Defines a setup-style store: couples a unique <paramref name="id"/> with a
    /// <paramref name="setup"/> delegate that constructs the store — the C# port of Pinia's
    /// <c>defineStore(id, setup)</c> (https://pinia.vuejs.org/core-concepts/#setup-stores,
    /// <c>packages/pinia/src/store.ts</c>). Defining a store does not run <paramref name="setup"/>;
    /// the first <see cref="StoreDefinition{TStore}.UseStore(StoreRegistry)"/> per registry does, and
    /// exactly once per registry.
    /// </summary>
    /// <typeparam name="TStore">The store instance type the setup produces.</typeparam>
    /// <param name="id">The store's unique id within any registry it is used in.</param>
    /// <param name="setup">The delegate that constructs the store inside its effect scope.</param>
    /// <returns>The typed store definition (the C# port of the <c>useStore</c> that <c>defineStore</c> returns).</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="setup"/> is null.</exception>
    public static StoreDefinition<TStore> DefineStore<TStore>(string id, StoreSetup<TStore> setup)
        where TStore : class
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(setup);
        return new StoreDefinition<TStore>(id, setup);
    }

    /// <summary>
    /// Creates a store registry — the per-app root that owns every store's lifetime, the C# port of
    /// Pinia's <c>createPinia()</c> (<c>packages/pinia/src/createPinia.ts</c>). Install it on an app
    /// with <c>App.Use(registry.AsPlugin())</c>, or pass it directly to
    /// <see cref="StoreDefinition{TStore}.UseStore(StoreRegistry)"/> for DI-style resolution.
    /// Equivalent to <c>new StoreRegistry()</c>.
    /// </summary>
    /// <returns>A fresh, empty registry.</returns>
    public static StoreRegistry CreateRegistry() => new();

    /// <summary>
    /// The ambient registry that <see cref="StoreDefinition{TStore}.UseStore()"/> falls back to
    /// outside a component context — the C# port of Pinia's <c>activePinia</c>
    /// (<c>packages/pinia/src/rootStore.ts</c>). Set when a registry is installed via <c>App.Use</c>.
    /// Ambient and NOT thread-safe; see the type remarks on server isolation.
    /// </summary>
    public static StoreRegistry? ActiveRegistry => _activeRegistry;

    /// <summary>
    /// Sets the ambient <see cref="ActiveRegistry"/> — the C# port of Pinia's
    /// <c>setActivePinia(pinia)</c>. Installing a registry via <c>App.Use</c> calls this; call it
    /// directly (or with <see langword="null"/> to clear) in tests or non-component bootstrap code.
    /// </summary>
    /// <param name="registry">The registry to make ambient, or null to clear.</param>
    public static void SetActiveRegistry(StoreRegistry? registry) => _activeRegistry = registry;
}
