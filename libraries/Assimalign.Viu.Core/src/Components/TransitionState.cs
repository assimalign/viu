using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// The shared per-<c>&lt;Transition&gt;</c>-instance state — the C# port of upstream's
/// <c>TransitionState</c> (<c>packages/runtime-core/src/components/BaseTransition.ts</c>,
/// https://vuejs.org/guide/built-ins/transition.html). Created once per transition component through
/// <see cref="BaseTransition.UseTransitionState"/> and threaded into every
/// <see cref="BaseTransition.ResolveTransitionHooks"/> call so enter/leave choreography can observe
/// the mount phase (<see cref="IsMounted"/> gates whether enter hooks run without <c>appear</c>),
/// the <c>out-in</c> leaving phase (<see cref="IsLeaving"/>), and teardown
/// (<see cref="IsUnmounting"/> makes a leave remove synchronously rather than animate).
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class TransitionState
{
    /// <summary>
    /// Whether the owning transition component has finished its first mount (upstream:
    /// <c>isMounted</c>). Set true by an <c>onMounted</c> hook; until then, enter hooks run only when
    /// <c>appear</c> is enabled.
    /// </summary>
    public bool IsMounted { get; internal set; }

    /// <summary>
    /// Whether an <c>out-in</c> transition is currently playing its leave before the incoming child
    /// mounts (upstream: <c>isLeaving</c>). While set, the transition renders an empty placeholder.
    /// </summary>
    public bool IsLeaving { get; internal set; }

    /// <summary>
    /// Whether the owning transition component is tearing down (upstream: <c>isUnmounting</c>). Set
    /// true by an <c>onBeforeUnmount</c> hook; a leave during unmount removes synchronously instead of
    /// animating (upstream parity).
    /// </summary>
    public bool IsUnmounting { get; internal set; }

    /// <summary>
    /// The per-type cache of currently-leaving vnodes, keyed by vnode type then by string key
    /// (upstream: <c>leavingVNodes</c>). Lets a re-entering element cancel the matching outgoing
    /// element's in-flight leave so the two never both animate. Populated by
    /// <see cref="BaseTransition.ResolveTransitionHooks"/>'s leave path.
    /// </summary>
    internal Dictionary<object, Dictionary<string, VirtualNode>> LeavingVirtualNodes { get; } = new();

    /// <summary>
    /// The in-flight enter callbacks keyed by platform element (the boxed renderer node) — the C#
    /// stand-in for upstream's <c>el[enterCbKey]</c>. Each value is the cancel-aware "done"
    /// (<c>done(cancelled)</c>) an interrupting leave invokes with <see langword="true"/> to cancel the
    /// enter. Entries clear themselves when the enter settles. The default comparer keys correctly for
    /// both DOM int handles (unique per element) and reference-typed test nodes.
    /// </summary>
    internal Dictionary<object, Action<bool>> EnterCallbacks { get; } = new();

    /// <summary>
    /// The in-flight leave callbacks keyed by platform element (the boxed renderer node) — the C#
    /// stand-in for upstream's <c>el[leaveCbKey]</c>. Each value is the cancel-aware "done"
    /// (<c>done(cancelled)</c>) a re-entering same-type element invokes to finish or cancel the leave.
    /// Entries clear themselves when the leave settles.
    /// </summary>
    internal Dictionary<object, Action<bool>> LeaveCallbacks { get; } = new();
}
