using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Defines setup-style state stores, creates explicitly owned registries, and manages the optional
/// ambient application registry. Specified by <c>[STA-1]</c> through <c>[STA-4]</c>.
/// </summary>
/// <remarks>
/// An individual registry remains single-event-loop and is not thread-safe. Browser uses one shared
/// ambient value; ServerRenderer selects a logical-flow-local value for each request. Other
/// multi-request hosts should pass an explicit request-owned registry. Specified by
/// <c>[EXE-1]</c>.
/// </remarks>
public static class StateStores
{
    /// <summary>
    /// Enters a fresh logical execution flow whose ambient setup context and active registry are
    /// independent of the caller's flow.
    /// </summary>
    /// <returns>
    /// An idempotent lease that restores the previous ambient state-store values when disposed.
    /// Nested leases restore their parent flow when disposed in last-in, first-out order.
    /// </returns>
    /// <remarks>
    /// Request-oriented hosts use this seam to prevent independently owned registries and setup
    /// operations from sharing ambient state. The lease does not create or dispose a registry;
    /// registry ownership remains explicit. Each entered flow remains single-event-loop and is not
    /// thread-safe. Specified by <c>[EXE-1]</c> and <c>[CMP-33]</c>.
    /// </remarks>
    public static IDisposable EnterExecutionFlow() => StateExecutionIsolation.Enter();

    /// <summary>
    /// Gets or sets the ambient registry used by argument-less resolution and as the fallback after
    /// component services. It is <see langword="null"/> by default. Specified by <c>[STA-4]</c>.
    /// </summary>
    public static IStateStoreRegistry? ActiveRegistry
    {
        get => StateExecutionIsolation.Current.ActiveRegistry;
        set => StateExecutionIsolation.Current.ActiveRegistry = value;
    }

    /// <summary>
    /// Sets the ambient registry, or clears it with <see langword="null"/>. This named form makes
    /// host bootstrap and teardown intent explicit. Specified by <c>[STA-4]</c>.
    /// </summary>
    /// <param name="registry">The registry to make active, or <see langword="null"/>.</param>
    public static void SetActiveRegistry(IStateStoreRegistry? registry)
        => StateExecutionIsolation.Current.ActiveRegistry = registry;

    /// <summary>
    /// Defines a context-aware state store without reflection-backed activation. Specified by
    /// <c>[STA-1]</c>.
    /// </summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="key">The non-empty application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe setup delegate.</param>
    /// <returns>Reusable registry-independent store metadata.</returns>
    public static StateStoreDefinition<TStore> Define<TStore>(
        string key,
        StateStoreActivator<TStore> setup)
        where TStore : class
        => new(key, setup);

    /// <summary>
    /// Defines a context-aware state store with an explicit AOT-safe payload serializer. Specified
    /// by <c>[STA-9]</c> and <c>[EXE-4]</c>.
    /// </summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="key">The non-empty application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe setup delegate.</param>
    /// <param name="serializer">The explicit AOT-safe state serializer.</param>
    /// <returns>Reusable registry-independent store metadata.</returns>
    public static StateStoreDefinition<TStore> Define<TStore>(
        string key,
        StateStoreActivator<TStore> setup,
        IStateStoreSerializer<TStore> serializer)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(serializer);
        return new StateStoreDefinition<TStore>(key, setup, serializer);
    }

    /// <summary>
    /// Defines a parameterless state store without reflection-backed activation. Specified by
    /// <c>[STA-1]</c>.
    /// </summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="key">The non-empty application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe setup delegate.</param>
    /// <returns>Reusable registry-independent store metadata.</returns>
    public static StateStoreDefinition<TStore> Define<TStore>(
        string key,
        Func<TStore> setup)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(setup);
        return new StateStoreDefinition<TStore>(key, _ => setup());
    }

    /// <summary>
    /// Defines a parameterless state store with an explicit AOT-safe payload serializer. Specified
    /// by <c>[STA-9]</c> and <c>[EXE-4]</c>.
    /// </summary>
    /// <typeparam name="TStore">The state store type.</typeparam>
    /// <param name="key">The non-empty application-unique state-store key.</param>
    /// <param name="setup">The explicit AOT-safe setup delegate.</param>
    /// <param name="serializer">The explicit AOT-safe state serializer.</param>
    /// <returns>Reusable registry-independent store metadata.</returns>
    public static StateStoreDefinition<TStore> Define<TStore>(
        string key,
        Func<TStore> setup,
        IStateStoreSerializer<TStore> serializer)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(serializer);
        return new StateStoreDefinition<TStore>(key, _ => setup(), serializer);
    }

    /// <summary>
    /// Creates an independent registry using Reactivity's production scope factory. Specified by
    /// <c>[STA-2]</c> and <c>[STA-3]</c>.
    /// </summary>
    /// <param name="services">The optional externally owned application service provider.</param>
    /// <param name="watchScheduler">
    /// The optional watch policy; <see langword="null"/> selects synchronous delivery.
    /// </param>
    /// <returns>The new registry, which the caller must dispose.</returns>
    public static IStateStoreRegistry CreateRegistry(
        IServiceProvider? services = null,
        IReactiveWatchScheduler? watchScheduler = null) =>
        new StateStoreRegistry(
            services,
            new ReactiveEffectScopeFactory(),
            watchScheduler);

    /// <summary>
    /// Creates an independent registry with an explicitly supplied reactive scope factory.
    /// Specified by <c>[STA-2]</c> and <c>[STA-3]</c>.
    /// </summary>
    /// <param name="effectScopes">The reactive effect-scope factory.</param>
    /// <param name="services">The optional externally owned application service provider.</param>
    /// <param name="watchScheduler">
    /// The optional watch policy; <see langword="null"/> selects synchronous delivery.
    /// </param>
    /// <returns>The new registry, which the caller must dispose.</returns>
    public static StateStoreRegistry CreateRegistry(
        IReactiveEffectScopeFactory effectScopes,
        IServiceProvider? services = null,
        IReactiveWatchScheduler? watchScheduler = null)
        => new(services, effectScopes, watchScheduler);
}
