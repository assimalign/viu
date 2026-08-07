using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Components;

/// <summary>
/// The authoring surface handed to <see cref="IComponent.Setup"/> for one mounted component
/// instance. Abstract here; the runtime provides the single implementation.
/// </summary>
/// <remarks>
/// This type deliberately carries no per-convention members: reactive scope and scheduling are
/// model vocabulary (a component is a reactive render function), while conventions layered on the
/// model — state stores, routing, and whatever comes next — reach the context only through
/// <see cref="Services"/> or the ambient reactive scope. No capability is discovered by casting
/// this type. A consumer-derived context is inert: no runtime API accepts one, and the runtime
/// only ever invokes <see cref="IComponent.Setup"/> with its own implementation.
/// Deliberately absent: a style-scope identifier — scoped CSS is deferred until after the
/// component-model arc; reintroduction is an additive member.
/// </remarks>
public abstract class ComponentContext
{
    /// <summary>Initializes the authoring surface. Runtime-implemented.</summary>
    protected ComponentContext()
    {
    }

    /// <summary>Gets the contract-resolved view of the parent-supplied invocation.</summary>
    public abstract ComponentBindings Bindings { get; }

    /// <summary>Gets the application-composed service provider, or null when none is configured.</summary>
    public abstract IServiceProvider? Services { get; }

    /// <summary>Gets the lifecycle registration surface for this mounted instance.</summary>
    public abstract ComponentLifecycle Lifecycle { get; }

    /// <summary>Gets the reactive scope that owns this instance's effects and watches.</summary>
    public abstract IReactiveEffectScope Scope { get; }

    /// <summary>Gets the scheduler batching watch callbacks, or null for synchronous fallback.</summary>
    public abstract IReactiveWatchScheduler? WatchScheduler { get; }

    /// <summary>Gets the parent component's context, or null at a root.</summary>
    public abstract ComponentContext? Parent { get; }

    /// <summary>Raises a declared event toward the parent-supplied listeners.</summary>
    /// <param name="name">The declared event name.</param>
    /// <param name="arguments">The event arguments.</param>
    public abstract void Emit(string name, params object?[] arguments);

    /// <summary>Publishes a value readable through this instance's mount reference.</summary>
    /// <param name="value">The exposed value.</param>
    public abstract void Expose(object? value);

    /// <summary>Routes a development-time warning through the application warning channel.</summary>
    /// <param name="message">The warning text.</param>
    public abstract void Warn(string message);

    /// <summary>
    /// Watches a tracked getter within this instance's scope and scheduler; the watch stops
    /// automatically on unmount. Implemented once here against <see cref="Scope"/> and
    /// <see cref="WatchScheduler"/>; failures route through <see cref="OnWatchError"/>.
    /// </summary>
    /// <typeparam name="TValue">The watched value type.</typeparam>
    /// <param name="getter">The tracked value getter.</param>
    /// <param name="callback">The callback receiving the current and previous values.</param>
    /// <returns>A handle that stops the watch.</returns>
    public WatchHandle Watch<TValue>(Func<TValue> getter, Action<TValue, TValue> callback)
        => throw new NotSupportedException("The contract model does not execute watches.");

    /// <summary>Receives a watch callback failure for component error routing.</summary>
    /// <param name="exception">The callback failure.</param>
    protected abstract void OnWatchError(Exception exception);
}
