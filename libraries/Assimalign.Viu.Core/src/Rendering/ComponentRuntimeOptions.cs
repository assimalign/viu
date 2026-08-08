using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Supplies externally owned composition objects used by the component host.
/// </summary>
/// <remarks>
/// Deliberately absent: any per-convention composition such as a state-store registry.
/// Conventions reach a mounted component only through <see cref="Services"/> or their own ambient
/// registration; they never earn an option or a context member.
/// </remarks>
public sealed class ComponentRuntimeOptions
{
    /// <summary>Initializes host composition.</summary>
    /// <param name="components">The registration-backed component factory.</param>
    /// <param name="watchScheduler">The component watch scheduling policy.</param>
    /// <param name="services">The optional externally owned application service provider.</param>
    /// <param name="errorHandler">
    /// The optional terminal sink for component errors not stopped by an ancestor capture hook.
    /// </param>
    /// <param name="warnHandler">The optional application warning sink.</param>
    /// <param name="eventObserver">
    /// The optional diagnostic observer notified after a component event is dispatched.
    /// </param>
    public ComponentRuntimeOptions(
        IComponentFactory components,
        IReactiveWatchScheduler watchScheduler,
        IServiceProvider? services = null,
        Action<Exception, ComponentContext?, string>? errorHandler = null,
        Action<string>? warnHandler = null,
        Action<ComponentContext, string, IReadOnlyList<object?>>? eventObserver = null)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(watchScheduler);
        Components = components;
        WatchScheduler = watchScheduler;
        Services = services;
        ErrorHandler = errorHandler;
        WarnHandler = warnHandler;
        EventObserver = eventObserver;
    }

    /// <summary>Gets the borrowed component factory.</summary>
    public IComponentFactory Components { get; }

    /// <summary>Gets the borrowed component watch scheduler.</summary>
    public IReactiveWatchScheduler WatchScheduler { get; }

    /// <summary>Gets the borrowed optional application service provider.</summary>
    public IServiceProvider? Services { get; }

    /// <summary>
    /// Gets the terminal sink for observed render, lifecycle, watcher, and event faults that no
    /// ancestor <see cref="ComponentLifecycle.OnErrorCaptured"/> callback stopped. A missing sink
    /// leaves such a fault unhandled and preserves its exception. Specified by <c>[CMP-23]</c>.
    /// </summary>
    public Action<Exception, ComponentContext?, string>? ErrorHandler { get; }

    /// <summary>
    /// Gets the application warning sink used for binding diagnostics, invalid event arguments,
    /// undeclared events, and explicit <see cref="ComponentContext.Warn"/> calls. Specified by
    /// <c>[CMP-12]</c> through <c>[CMP-14]</c>.
    /// </summary>
    public Action<string>? WarnHandler { get; }

    /// <summary>
    /// Gets the diagnostic observer notified synchronously after an emitted event is dispatched.
    /// The observer cannot replace or suppress the listener path. Promoted as the public testing
    /// seam by <c>[V01.01.15.02]</c>.
    /// </summary>
    public Action<ComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; }
}
