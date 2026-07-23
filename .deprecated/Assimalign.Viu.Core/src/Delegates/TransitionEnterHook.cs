using System;

namespace Assimalign.Viu;

/// <summary>
/// An asynchronous enter/leave transition hook — the C# port of the <c>(el, done) =&gt; void</c>
/// hook shape of upstream's <c>BaseTransitionProps</c> (<c>onEnter</c>, <c>onLeave</c>,
/// <c>onAppear</c>; <c>packages/runtime-core/src/components/BaseTransition.ts</c>). The hook receives
/// the platform element (the boxed renderer node) and a <paramref name="done"/> callback it must
/// invoke exactly once when the transition phase completes (upstream fires the corresponding
/// <c>afterEnter</c>/<c>afterLeave</c> and, for enter, any deferred <c>in-out</c> leave).
/// <para>
/// Deviates from upstream Vue 3 parity per design decision: upstream inspects the hook's declared
/// arity to decide whether to auto-invoke <c>done</c> for a hook that omits the callback. C#
/// delegates cannot be arity-inspected, so a Viu enter/leave hook always receives
/// <paramref name="done"/> and is responsible for invoking it — the DOM
/// <see cref="Assimalign.Viu.BaseTransition"/> wrapper adapts a fire-and-forget
/// <see cref="Action{T}"/> user hook into this shape by invoking <paramref name="done"/> itself.
/// </para>
/// </summary>
/// <param name="element">The platform element the transition runs on (the boxed renderer node).</param>
/// <param name="done">Invoked once when this enter/leave phase finishes.</param>
public delegate void TransitionEnterHook(object element, Action done);
