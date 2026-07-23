using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Defines setup-style state stores and manages the optional ambient application registry.
/// </summary>
/// <remarks>
/// Ambient registry state is not thread-safe and targets Viu's single-threaded event-loop model.
/// Server and multi-request hosts should pass an explicit registry instead.
/// </remarks>
public static class StateStores
{
    private static IStateStoreRegistry? _activeRegistry;

    /// <summary>Gets the ambient state registry used by argument-less definition resolution.</summary>
    public static IStateStoreRegistry? ActiveRegistry => _activeRegistry;

    /// <summary>Sets the ambient state registry, or clears it with null.</summary>
    /// <param name="registry">The registry to make active, or null.</param>
    public static void SetActiveRegistry(IStateStoreRegistry? registry)
        => _activeRegistry = registry;

    /// <summary>Defines a context-aware, setup-style state store.</summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="key">The application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe setup delegate.</param>
    /// <returns>The reusable state-store definition.</returns>
    public static StateStoreDefinition<TStore> Define<TStore>(
        string key,
        StateStoreSetup<TStore> setup)
        where TStore : class
        => new(key, setup);

    /// <summary>Defines a parameterless setup-style state store.</summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="key">The application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe setup delegate.</param>
    /// <returns>The reusable state-store definition.</returns>
    public static StateStoreDefinition<TStore> Define<TStore>(
        string key,
        Func<TStore> setup)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(setup);
        return new StateStoreDefinition<TStore>(key, _ => setup());
    }

    /// <summary>Creates a per-application state registry.</summary>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The application-selected service resolver.</param>
    /// <param name="effectScopes">The reactive effect-scope factory.</param>
    /// <param name="watchScheduler">
    /// The application watch scheduler, or null for synchronous standalone behavior.
    /// </param>
    /// <returns>A fresh state registry.</returns>
    public static StateStoreRegistry CreateRegistry(
        IComponentFactory components,
        IServiceProvider services,
        IReactiveEffectScopeFactory effectScopes,
        IReactiveWatchScheduler? watchScheduler = null)
        => new(components, services, effectScopes, watchScheduler);
}
