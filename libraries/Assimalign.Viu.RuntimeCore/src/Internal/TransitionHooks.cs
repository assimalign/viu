using System;
using System.Collections.Generic;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The resolved enter/leave hook set the renderer runs for one transitioning vnode — the C# port of
/// upstream's <c>TransitionHooks</c> object produced by <c>resolveTransitionHooks</c>
/// (<c>packages/runtime-core/src/components/BaseTransition.ts</c>). Stamped onto a vnode as
/// <see cref="VirtualNode.Transition"/> by <see cref="BaseTransition.SetTransitionHooks"/>; the
/// renderer calls <see cref="BeforeEnter"/> + <see cref="Enter"/> around mount insertion and
/// <see cref="Leave"/> in place of a direct remove (see <see cref="Renderer{TNode}"/>). The
/// per-element in-flight callbacks live on the shared <see cref="TransitionState"/> (the C# stand-in
/// for upstream's <c>el[enterCbKey]</c>/<c>el[leaveCbKey]</c>), so a re-entering element can cancel a
/// matching outgoing element's leave. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal sealed class TransitionHooks
{
    private readonly VirtualNode _vnode;
    private readonly BaseTransitionProperties _properties;
    private readonly TransitionState _state;
    private readonly ComponentInstance _instance;
    private readonly Action<TransitionHooks>? _postClone;
    private readonly string _key;
    private readonly Dictionary<string, VirtualNode> _leavingVirtualNodesCache;

    internal TransitionHooks(
        VirtualNode vnode,
        BaseTransitionProperties properties,
        TransitionState state,
        ComponentInstance instance,
        Action<TransitionHooks>? postClone)
    {
        _vnode = vnode;
        _properties = properties;
        _state = state;
        _instance = instance;
        _postClone = postClone;
        // Upstream String(vnode.key): a keyless vnode resolves to a single stable slot per type.
        _key = vnode.Key?.ToString() ?? "undefined";
        _leavingVirtualNodesCache = BaseTransition.GetLeavingNodesForType(state, vnode);
    }

    /// <summary>The transition mode (upstream: <c>hooks.mode</c>): <c>"in-out"</c>, <c>"out-in"</c>, or null.</summary>
    internal string? Mode => _properties.Mode;

    /// <summary>Whether the element is persisted (v-show) rather than inserted/removed (upstream: <c>hooks.persisted</c>).</summary>
    internal bool Persisted => _properties.Persisted;

    /// <summary>
    /// The <c>out-in</c> continuation the renderer's remove path runs after the outgoing element's
    /// leave completes (upstream: <c>hooks.afterLeave</c>), re-rendering the transition to mount the
    /// incoming child. Set by <see cref="BaseTransition"/>'s render mode handling.
    /// </summary>
    internal Action? AfterLeave { get; set; }

    /// <summary>
    /// The <c>in-out</c> leave-delay hook (upstream: <c>hooks.delayLeave</c>). When set, the renderer
    /// defers the outgoing element's leave to it instead of leaving immediately.
    /// </summary>
    internal TransitionDelayLeave? DelayLeave { get; set; }

    /// <summary>
    /// The <c>in-out</c> deferred-leave continuation fired once the incoming element's enter finishes
    /// (upstream: <c>hooks.delayedLeave</c>).
    /// </summary>
    internal Action? DelayedLeave { get; set; }

    /// <summary>
    /// Runs before the element is inserted (upstream: <c>beforeEnter</c>). Cancels an in-flight leave
    /// on the same element, force-finishes a matching same-type outgoing element's leave (the v-if
    /// toggle case), then fires the before-enter (or before-appear) hook. A no-op on the initial
    /// mount unless <c>appear</c> is set.
    /// </summary>
    /// <param name="element">The platform element being inserted (the boxed renderer node).</param>
    internal void BeforeEnter(object element)
    {
        var hook = _properties.OnBeforeEnter;
        if (!_state.IsMounted)
        {
            if (_properties.Appear)
            {
                hook = _properties.OnBeforeAppear ?? _properties.OnBeforeEnter;
            }
            else
            {
                return;
            }
        }
        // Cancel this element's own in-flight leave (re-entering while leaving).
        if (_state.LeaveCallbacks.TryGetValue(element, out var ownLeave))
        {
            ownLeave(true);
        }
        // Force the matching same-type outgoing element to finish leaving (upstream: no cancelled
        // arg -> the afterLeave path runs, removing it) so the two never coexist.
        if (_leavingVirtualNodesCache.TryGetValue(_key, out var leavingVirtualNode)
            && BaseTransition.IsSameVirtualNodeType(_vnode, leavingVirtualNode)
            && leavingVirtualNode.El is { } leavingElement
            && _state.LeaveCallbacks.TryGetValue(leavingElement, out var leavingDone))
        {
            leavingDone(false);
        }
        hook?.Invoke(element);
    }

    /// <summary>
    /// Runs after the element is inserted (upstream: <c>enter</c>). Runs the enter (or appear) hook
    /// and, when it signals completion, fires the after-enter (or cancelled) hook and any deferred
    /// <c>in-out</c> leave. A no-op on the initial mount unless <c>appear</c> is set.
    /// </summary>
    /// <param name="element">The inserted platform element (the boxed renderer node).</param>
    internal void Enter(object element)
    {
        var hook = _properties.OnEnter;
        var afterHook = _properties.OnAfterEnter;
        var cancelHook = _properties.OnEnterCancelled;
        if (!_state.IsMounted)
        {
            if (_properties.Appear)
            {
                hook = _properties.OnAppear ?? _properties.OnEnter;
                afterHook = _properties.OnAfterAppear ?? _properties.OnAfterEnter;
                cancelHook = _properties.OnAppearCancelled ?? _properties.OnEnterCancelled;
            }
            else
            {
                return;
            }
        }
        var called = false;
        void Done(bool cancelled)
        {
            if (called)
            {
                return;
            }
            called = true;
            if (cancelled)
            {
                cancelHook?.Invoke(element);
            }
            else
            {
                afterHook?.Invoke(element);
            }
            // Fire a deferred in-out leave now that the incoming element has entered.
            DelayedLeave?.Invoke();
            _state.EnterCallbacks.Remove(element);
        }
        _state.EnterCallbacks[element] = Done;
        if (hook is not null)
        {
            hook(element, () => Done(false));
        }
        else
        {
            Done(false);
        }
    }

    /// <summary>
    /// Runs the leave transition in place of a direct remove (upstream: <c>leave</c>). Cancels an
    /// in-flight enter, then — unless the transition component itself is unmounting — runs the leave
    /// hook and removes the element via <paramref name="remove"/> when the hook signals completion.
    /// </summary>
    /// <param name="element">The leaving platform element (the boxed renderer node).</param>
    /// <param name="remove">Removes the element from the host; invoked once the leave completes.</param>
    internal void Leave(object element, Action remove)
    {
        // Cancel an in-flight enter on this element (toggled back out mid-enter).
        if (_state.EnterCallbacks.TryGetValue(element, out var enterDone))
        {
            enterDone(true);
        }
        if (_state.IsUnmounting)
        {
            remove();
            return;
        }
        _properties.OnBeforeLeave?.Invoke(element);
        var called = false;
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
            if (_leavingVirtualNodesCache.TryGetValue(_key, out var cached) && ReferenceEquals(cached, _vnode))
            {
                _leavingVirtualNodesCache.Remove(_key);
            }
        }
        _state.LeaveCallbacks[element] = Done;
        _leavingVirtualNodesCache[_key] = _vnode;
        if (_properties.OnLeave is not null)
        {
            _properties.OnLeave(element, () => Done(false));
        }
        else
        {
            Done(false);
        }
    }

    /// <summary>
    /// Resolves a fresh hook set for a re-render of the same transition (upstream: <c>clone</c>),
    /// carrying the shared props/state/instance and re-applying the render's <c>postClone</c> capture.
    /// </summary>
    /// <param name="vnode">The vnode the cloned hooks bind to.</param>
    /// <returns>The cloned hooks.</returns>
    internal TransitionHooks Clone(VirtualNode vnode)
    {
        var hooks = BaseTransition.ResolveTransitionHooks(vnode, _properties, _state, _instance, _postClone);
        _postClone?.Invoke(hooks);
        return hooks;
    }
}
