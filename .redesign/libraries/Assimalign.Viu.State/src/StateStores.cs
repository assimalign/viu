using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Creates explicitly owned state-store registries without ambient process-wide state.
/// </summary>
public static class StateStores
{
    /// <summary>
    /// Gets or sets the ambient active registry used when a component's application services do
    /// not carry one. Explicitly assigned by the application owner; null by default.
    /// </summary>
    /// <remarks>Single-threaded ambient state intended for the host event loop.</remarks>
    public static IStateStoreRegistry? ActiveRegistry { get; set; }

    /// <summary>Creates an independent registry.</summary>
    /// <param name="services">The optional externally owned application service provider.</param>
    /// <param name="watchScheduler">The optional watch policy; synchronous delivery is the default.</param>
    /// <returns>The new registry, which the caller must dispose.</returns>
    public static IStateStoreRegistry CreateRegistry(
        IServiceProvider? services = null,
        IReactiveWatchScheduler? watchScheduler = null) =>
        new StateStoreRegistry(
            services,
            watchScheduler ?? new ImmediateWatchScheduler());
}
