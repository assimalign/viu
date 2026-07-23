using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class TransitionHooks
{
    private readonly BaseTransitionProperties _properties;
    private readonly TransitionState _state;
    private readonly TransitionIdentity _identity;
    private ComponentTransition? _componentTransition;

    internal TransitionHooks(
        IComponent component,
        BaseTransitionProperties properties,
        TransitionState state)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(state);
        _properties = properties;
        _state = state;
        _identity = TransitionComponents.Identity(component);
    }

    internal bool Persisted => _properties.Persisted;

    internal ComponentTransition ComponentTransition =>
        _componentTransition ??= new ComponentTransition(this);

    internal Action? AfterLeave { get; set; }

    internal TransitionDelayLeave? DelayLeave { get; set; }

    internal Action? DelayedLeave { get; set; }

    internal void BeforeEnter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        Action<object>? hook = _properties.OnBeforeEnter;
        if (!_state.IsMounted)
        {
            if (!_properties.Appear)
            {
                return;
            }

            hook = _properties.OnBeforeAppear ?? hook;
        }

        if (_state.LeaveCallbacks.TryGetValue(element, out Action<bool>? ownLeave))
        {
            ownLeave(true);
        }

        if (_state.Leaving.TryGetValue(_identity, out TransitionHooks? leaving)
            && leaving.TryFinishLeave(cancelled: false))
        {
            _state.Leaving.Remove(_identity);
        }

        hook?.Invoke(element);
    }

    internal void Enter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        TransitionEnterHook? hook = _properties.OnEnter;
        Action<object>? afterHook = _properties.OnAfterEnter;
        Action<object>? cancelledHook = _properties.OnEnterCancelled;
        if (!_state.IsMounted)
        {
            if (!_properties.Appear)
            {
                return;
            }

            hook = _properties.OnAppear ?? hook;
            afterHook = _properties.OnAfterAppear ?? afterHook;
            cancelledHook = _properties.OnAppearCancelled ?? cancelledHook;
        }

        bool called = false;
        void Done(bool cancelled)
        {
            if (called)
            {
                return;
            }

            called = true;
            if (cancelled)
            {
                cancelledHook?.Invoke(element);
            }
            else
            {
                afterHook?.Invoke(element);
            }

            _state.EnterCallbacks.Remove(element);
            Action? delayedLeave = DelayedLeave;
            DelayedLeave = null;
            delayedLeave?.Invoke();
        }

        _state.EnterCallbacks[element] = Done;
        if (hook is null)
        {
            Done(false);
        }
        else
        {
            hook(element, () => Done(false));
        }
    }

    internal void Leave(object element, Action remove)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(remove);
        if (_state.EnterCallbacks.TryGetValue(element, out Action<bool>? enter))
        {
            enter(true);
        }

        if (_state.IsUnmounting)
        {
            remove();
            return;
        }

        _properties.OnBeforeLeave?.Invoke(element);
        bool called = false;
        void Done(bool cancelled)
        {
            if (called)
            {
                return;
            }

            called = true;
            remove();
            if (cancelled)
            {
                _properties.OnLeaveCancelled?.Invoke(element);
            }
            else
            {
                _properties.OnAfterLeave?.Invoke(element);
            }

            _state.LeaveCallbacks.Remove(element);
            _state.Leaving.Remove(_identity);
            AfterLeave?.Invoke();
        }

        _state.LeaveCallbacks[element] = Done;
        _state.Leaving[_identity] = this;
        _finishLeave = Done;
        if (_properties.OnLeave is null)
        {
            Done(false);
        }
        else
        {
            _properties.OnLeave(element, () => Done(false));
        }
    }

    private Action<bool>? _finishLeave;

    private bool TryFinishLeave(bool cancelled)
    {
        Action<bool>? finish = _finishLeave;
        if (finish is null)
        {
            return false;
        }

        _finishLeave = null;
        finish(cancelled);
        return true;
    }
}
