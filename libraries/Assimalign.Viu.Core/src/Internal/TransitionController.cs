using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class TransitionController
{
    private readonly TransitionIdentity _identity;
    private readonly Func<Action, string, bool> _invoke;
    private readonly TransitionState _state;
    private readonly Dictionary<object, bool> _preparedAppearances = [];
    private ComponentTransition? _componentTransition;
    private object? _enterElement;
    private Action<bool>? _enterFinish;
    private object? _leaveElement;
    private Action<bool>? _leaveFinish;
    private bool _isDisposed;

    internal TransitionController(
        TransitionProperties properties,
        TransitionState state,
        TransitionIdentity identity,
        Func<Action, string, bool> invoke)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(invoke);
        Properties = properties;
        _state = state;
        _identity = identity;
        _invoke = invoke;
    }

    internal TransitionProperties Properties { get; private set; }

    internal ComponentTransition ComponentTransition =>
        _componentTransition ??= new ComponentTransition(this);

    internal bool IsHydrating { get; set; }

    internal void UpdateProperties(TransitionProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Properties = properties;
    }

    internal bool BeforeEnter(object element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_isDisposed || IsHydrating)
        {
            return false;
        }

        bool isAppear = !_state.IsMounted;
        _preparedAppearances[element] = isAppear;
        if (isAppear && !Properties.Appear)
        {
            return false;
        }

        if (_state.LeaveCallbacks.TryGetValue(element, out Action<bool>? ownLeave))
        {
            ownLeave(true);
        }

        if (_state.Leaving.TryGetValue(_identity, out Action<bool>? identityLeave))
        {
            identityLeave(false);
        }

        Action<object>? hook = isAppear
            ? Properties.OnBeforeAppear ?? Properties.OnBeforeEnter
            : Properties.OnBeforeEnter;
        Invoke(hook, element, isAppear
            ? "transition before-appear hook"
            : "transition before-enter hook");
        return true;
    }

    internal void Enter(object element, Action<bool> completion)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(completion);
        bool isAppear = _preparedAppearances.Remove(element, out bool prepared)
            ? prepared
            : !_state.IsMounted;
        if (_isDisposed || IsHydrating || (isAppear && !Properties.Appear))
        {
            completion(false);
            return;
        }

        if (_state.EnterCallbacks.TryGetValue(element, out Action<bool>? previousEnter))
        {
            previousEnter(true);
        }

        TransitionPhaseHook? hook = isAppear
            ? Properties.OnAppear ?? Properties.OnEnter
            : Properties.OnEnter;
        Action<object>? afterHook = isAppear
            ? Properties.OnAfterAppear ?? Properties.OnAfterEnter
            : Properties.OnAfterEnter;
        Action<object>? cancelledHook = isAppear
            ? Properties.OnAppearCancelled ?? Properties.OnEnterCancelled
            : Properties.OnEnterCancelled;
        bool isFinished = false;
        bool isInvokingHook = false;
        TransitionContinuationException? continuationFailure = null;
        Action<bool>? finish = null;
        finish = cancelled =>
        {
            if (isFinished)
            {
                return;
            }

            try
            {
                isFinished = true;
                if (_state.EnterCallbacks.TryGetValue(element, out Action<bool>? active)
                    && ReferenceEquals(active, finish))
                {
                    _state.EnterCallbacks.Remove(element);
                }

                if (ReferenceEquals(_enterFinish, finish))
                {
                    _enterFinish = null;
                    _enterElement = null;
                }

                Invoke(
                    cancelled ? cancelledHook : afterHook,
                    element,
                    cancelled
                        ? "transition enter-cancelled hook"
                        : "transition after-enter hook");
                completion(cancelled);
            }
            catch (Exception exception) when (isInvokingHook)
            {
                continuationFailure ??=
                    exception as TransitionContinuationException
                    ?? new TransitionContinuationException(exception);
                throw continuationFailure;
            }
        };
        _enterElement = element;
        _enterFinish = finish;
        _state.EnterCallbacks[element] = finish;
        if (hook is null)
        {
            finish(false);
            return;
        }

        bool invoked;
        isInvokingHook = true;
        try
        {
            invoked = TryInvoke(
                () => hook(element, () => finish(false)),
                isAppear ? "transition appear hook" : "transition enter hook");
        }
        catch (TransitionContinuationException exception)
        {
            exception.DispatchInformation.Throw();
            throw;
        }
        finally
        {
            isInvokingHook = false;
        }

        continuationFailure?.DispatchInformation.Throw();
        if (!invoked)
        {
            finish(false);
        }
    }

    internal void Leave(
        object element,
        Action<bool> removal,
        Action<bool>? afterCompletion = null,
        bool routeRemovalFailure = false)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(removal);
        if (_state.EnterCallbacks.TryGetValue(element, out Action<bool>? enter))
        {
            enter(true);
        }

        if (_isDisposed || IsHydrating || _state.IsUnmounting)
        {
            try
            {
                InvokeRemoval(removal, cancelled: false, routeRemovalFailure);
            }
            finally
            {
                afterCompletion?.Invoke(false);
            }

            return;
        }

        Invoke(Properties.OnBeforeLeave, element, "transition before-leave hook");
        Action<object>? afterHook = Properties.OnAfterLeave;
        Action<object>? cancelledHook = Properties.OnLeaveCancelled;
        bool isFinished = false;
        bool isInvokingHook = false;
        TransitionContinuationException? continuationFailure = null;
        Action<bool>? finish = null;
        finish = cancelled =>
        {
            if (isFinished)
            {
                return;
            }

            try
            {
                isFinished = true;
                if (_state.LeaveCallbacks.TryGetValue(element, out Action<bool>? active)
                    && ReferenceEquals(active, finish))
                {
                    _state.LeaveCallbacks.Remove(element);
                }

                if (_state.Leaving.TryGetValue(_identity, out Action<bool>? leaving)
                    && ReferenceEquals(leaving, finish))
                {
                    _state.Leaving.Remove(_identity);
                }

                if (ReferenceEquals(_leaveFinish, finish))
                {
                    _leaveFinish = null;
                    _leaveElement = null;
                }

                try
                {
                    InvokeRemoval(removal, cancelled, routeRemovalFailure);
                }
                finally
                {
                    try
                    {
                        Invoke(
                            cancelled ? cancelledHook : afterHook,
                            element,
                            cancelled
                                ? "transition leave-cancelled hook"
                                : "transition after-leave hook");
                    }
                    finally
                    {
                        afterCompletion?.Invoke(cancelled);
                    }
                }
            }
            catch (Exception exception) when (isInvokingHook)
            {
                continuationFailure ??=
                    exception as TransitionContinuationException
                    ?? new TransitionContinuationException(exception);
                throw continuationFailure;
            }
        };
        _leaveElement = element;
        _leaveFinish = finish;
        _state.LeaveCallbacks[element] = finish;
        _state.Leaving[_identity] = finish;
        if (Properties.OnLeave is null)
        {
            finish(false);
            return;
        }

        bool invoked;
        isInvokingHook = true;
        try
        {
            invoked = TryInvoke(
                () => Properties.OnLeave(element, () => finish(false)),
                "transition leave hook");
        }
        catch (TransitionContinuationException exception)
        {
            exception.DispatchInformation.Throw();
            throw;
        }
        finally
        {
            isInvokingHook = false;
        }

        continuationFailure?.DispatchInformation.Throw();
        if (!invoked)
        {
            finish(false);
        }
    }

    internal bool FinishEnter(object element, bool cancelled)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_enterFinish is null || !Equals(_enterElement, element))
        {
            return false;
        }

        _enterFinish(cancelled);
        return true;
    }

    internal bool FinishLeave(object element, bool cancelled)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (_leaveFinish is null || !Equals(_leaveElement, element))
        {
            return false;
        }

        _leaveFinish(cancelled);
        return true;
    }

    internal void Drain()
    {
        _enterFinish?.Invoke(true);
        _leaveFinish?.Invoke(false);
    }

    internal void Dispose()
    {
        Drain();
        _preparedAppearances.Clear();
        _isDisposed = true;
    }

    internal void ObserveBeforeUpdate(IReadOnlyList<TransitionElementSnapshot> snapshot) =>
        Invoke(
            Properties.OnBeforeUpdate,
            snapshot,
            "transition before-update observer");

    internal void ObserveUpdated(IReadOnlyList<TransitionElementSnapshot> snapshot) =>
        Invoke(Properties.OnUpdated, snapshot, "transition updated observer");

    private void Invoke<TValue>(
        Action<TValue>? hook,
        TValue value,
        string diagnosticInformation)
    {
        if (hook is not null)
        {
            _ = TryInvoke(() => hook(value), diagnosticInformation);
        }
    }

    private void InvokeRemoval(
        Action<bool> removal,
        bool cancelled,
        bool routeFailure)
    {
        if (routeFailure)
        {
            _ = TryInvoke(
                () => removal(cancelled),
                "transition removal callback");
        }
        else
        {
            removal(cancelled);
        }
    }

    private bool TryInvoke(Action callback, string diagnosticInformation)
    {
        return _invoke(callback, diagnosticInformation);
    }
}
