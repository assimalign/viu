using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Shares transition identity and cancellation state across component-owned children.</summary>
/// <remarks>
/// Host transition-group components use one scope per mounted component. The scope owns no layout
/// behavior; keyed snapshots still arrive through <see cref="TransitionProperties"/> observers.
/// This type is not thread-safe. Specified by <c>[BLT-9]</c>.
/// </remarks>
public sealed class ComponentTransitionScope
{
    private readonly TransitionState _state = new();

    /// <summary>Initializes a scope bound to one mounted component lifecycle.</summary>
    /// <param name="context">The component that owns the transitioned children.</param>
    public ComponentTransitionScope(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Lifecycle.OnMounted(() => _state.IsMounted = true);
        context.Lifecycle.OnBeforeUnmount(() => _state.IsUnmounting = true);
    }

    /// <summary>Wraps an already-created immutable child with this scope's shared state.</summary>
    /// <param name="child">The immutable child; use the slot overload to defer its creation.</param>
    /// <param name="properties">The resolved host transition behavior.</param>
    /// <param name="key">The optional transition-wrapper key.</param>
    /// <returns>A structural transition node carrying the behavior through its invocation.</returns>
    public TransitionNode Attach(
        VirtualNode child,
        TransitionProperties properties,
        object? key = null)
    {
        ArgumentNullException.ThrowIfNull(child);
        return Attach(_ => child, properties, key);
    }

    /// <summary>
    /// Wraps one lazy default slot with resolved behavior and this scope's shared state.
    /// </summary>
    /// <param name="child">The slot that creates the child only when Core evaluates the transition.</param>
    /// <param name="properties">The resolved host transition behavior.</param>
    /// <param name="key">The optional transition-wrapper key.</param>
    /// <returns>A structural transition node carrying the lazy slot through its invocation.</returns>
    public TransitionNode Attach(
        ComponentSlot child,
        TransitionProperties properties,
        object? key = null)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(properties);
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal)
        {
            [TransitionProperties.ResolvedArgument] = properties,
            [TransitionProperties.StateArgument] = _state,
        };
        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal)
        {
            ["default"] = child,
        };
        return new TransitionNode(new ComponentInvocation(arguments, slots), key);
    }

    /// <summary>Completes a pending enter before the host performs layout measurement.</summary>
    /// <param name="element">The opaque host element.</param>
    /// <returns><see langword="true"/> when a pending enter was completed.</returns>
    public bool FinishPendingEnter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_state.EnterCallbacks.TryGetValue(element, out Action<bool>? finish))
        {
            return false;
        }

        finish(false);
        return true;
    }
}
