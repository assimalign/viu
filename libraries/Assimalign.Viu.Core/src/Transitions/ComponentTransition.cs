using System;

namespace Assimalign.Viu;

/// <summary>
/// Exposes the resolved transition attached to an element directive binding.
/// </summary>
/// <remarks>
/// Directives such as browser <c>v-show</c> use this host-neutral facade to coordinate persisted
/// transitions. The renderer owns the underlying transition state.
/// </remarks>
public sealed class ComponentTransition
{
    private readonly TransitionHooks _hooks;

    internal ComponentTransition(TransitionHooks hooks)
    {
        _hooks = hooks;
    }

    /// <summary>Gets whether the transition keeps the element mounted while visibility changes.</summary>
    public bool IsPersisted => _hooks.Persisted;

    /// <summary>Runs the transition's before-enter phase.</summary>
    /// <param name="element">The opaque platform element.</param>
    public void BeforeEnter(object element)
    {
        _hooks.BeforeEnter(element);
    }

    /// <summary>Runs the transition's enter or appear phase.</summary>
    /// <param name="element">The opaque platform element.</param>
    public void Enter(object element)
    {
        _hooks.Enter(element);
    }

    /// <summary>Runs the leave phase and invokes <paramref name="remove"/> when it completes.</summary>
    /// <param name="element">The opaque platform element.</param>
    /// <param name="remove">The action performed after the leave phase.</param>
    public void Leave(object element, Action remove)
    {
        _hooks.Leave(element, remove);
    }
}
