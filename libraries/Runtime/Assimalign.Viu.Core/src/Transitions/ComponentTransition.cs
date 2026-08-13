using System;

namespace Assimalign.Viu;

/// <summary>Exposes a transition's resolved host-neutral state to an element directive.</summary>
/// <remarks>
/// Persisted directives use this facade to coordinate visibility without renderer-owned insertion
/// or removal. Completion is idempotent and callback failures use the owning component's error
/// route. Specified by <c>[BLT-10]</c>.
/// </remarks>
public sealed class ComponentTransition
{
    private readonly TransitionController _controller;

    internal ComponentTransition(TransitionController controller)
    {
        _controller = controller;
    }

    /// <summary>Gets whether the directive owns visibility changes for the mounted element.</summary>
    public bool IsPersisted => _controller.Properties.Persisted;

    /// <summary>Runs the resolved before-enter or before-appear callback.</summary>
    /// <param name="element">The opaque host element.</param>
    public void BeforeEnter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _controller.BeforeEnter(element);
    }

    /// <summary>Starts the resolved enter or appear phase.</summary>
    /// <param name="element">The opaque host element.</param>
    public void Enter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _controller.Enter(element, static _ => { });
    }

    /// <summary>Starts leave and invokes the removal action exactly once when it completes.</summary>
    /// <param name="element">The opaque host element.</param>
    /// <param name="remove">The directive-owned visibility or removal action.</param>
    public void Leave(object element, Action remove)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(remove);
        _controller.Leave(
            element,
            _ => remove(),
            routeRemovalFailure: true);
    }
}
