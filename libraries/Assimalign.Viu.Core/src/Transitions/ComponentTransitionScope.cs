using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Shares host-neutral transition state across multiple children owned by one component.
/// </summary>
/// <remarks>
/// This is the Core half of Vue 3.5's TransitionGroup choreography. Host packages retain ownership
/// of layout measurement and visual effects. The scope is component-local and is not thread-safe.
/// </remarks>
public sealed class ComponentTransitionScope
{
    private readonly TransitionState _state = new();

    /// <summary>Creates a transition scope bound to one mounted component lifecycle.</summary>
    /// <param name="context">The component that owns the transitioned children.</param>
    public ComponentTransitionScope(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Lifecycle.OnMounted(() => _state.IsMounted = true);
        context.Lifecycle.OnBeforeUnmount(
            () => _state.IsUnmounting = true);
    }

    /// <summary>Attaches a transition using this scope's shared state.</summary>
    /// <param name="child">The immutable child description.</param>
    /// <param name="properties">The resolved host-specific transition callbacks.</param>
    /// <returns>A transitioned description of <paramref name="child"/>.</returns>
    public IComponent Attach(
        IComponent child,
        BaseTransitionProperties properties)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(properties);
        return TransitionComponents.Attach(
            child,
            new TransitionHooks(child, properties, _state));
    }

    /// <summary>
    /// Completes an in-progress enter phase for a host element before the host measures it.
    /// </summary>
    /// <param name="element">The opaque host element.</param>
    /// <returns><see langword="true"/> when a pending enter phase was completed.</returns>
    public bool FinishPendingEnter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_state.EnterCallbacks.TryGetValue(
            element,
            out Action<bool>? finish))
        {
            return false;
        }

        finish(false);
        return true;
    }
}
