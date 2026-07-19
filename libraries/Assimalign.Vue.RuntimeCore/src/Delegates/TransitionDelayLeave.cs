using System;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The <c>in-out</c> mode leave-delay hook — the C# port of upstream's
/// <c>delayLeave(el, earlyRemove, delayedLeave)</c> on <c>TransitionHooks</c>
/// (<c>packages/runtime-core/src/components/BaseTransition.ts</c>). When an <c>&lt;Transition
/// mode="in-out"&gt;</c> swaps children, the outgoing element's leave is deferred: the renderer
/// hands this hook the outgoing element and two continuations — <paramref name="earlyRemove"/> to
/// remove it immediately (used when the same element re-enters before the delayed leave runs) and
/// <paramref name="delayedLeave"/> to start the real leave once the incoming element has entered.
/// </summary>
/// <param name="element">The outgoing platform element (the boxed renderer node).</param>
/// <param name="earlyRemove">Removes the outgoing element immediately.</param>
/// <param name="delayedLeave">Starts the deferred leave (fired after the incoming element enters).</param>
public delegate void TransitionDelayLeave(object element, Action earlyRemove, Action delayedLeave);
