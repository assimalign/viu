using System;

namespace Assimalign.Viu;

/// <summary>
/// The configuration a <see cref="BaseTransition"/> resolves its enter/leave choreography from — the
/// C# port of upstream's <c>BaseTransitionProps</c>
/// (<c>packages/runtime-core/src/components/BaseTransition.ts</c>,
/// https://vuejs.org/guide/built-ins/transition.html#javascript-hooks). The DOM
/// <c>&lt;Transition&gt;</c>/<c>&lt;TransitionGroup&gt;</c> build one of these from their CSS-class
/// props (upstream <c>resolveTransitionProps</c>) and pass it to <see cref="BaseTransition"/>; a
/// caller using <see cref="BaseTransition"/> directly supplies the JS hooks itself.
/// <para>
/// The enter/leave/appear hooks (<see cref="OnEnter"/>, <see cref="OnLeave"/>, <see cref="OnAppear"/>)
/// are <see cref="TransitionEnterHook"/>s: each receives a <c>done</c> callback it must invoke when
/// the phase completes (see that delegate's remarks for the arity divergence from upstream). The
/// remaining hooks are one-argument notifications. Not thread-safe (single-threaded JS event-loop
/// model).
/// </para>
/// </summary>
public sealed class BaseTransitionProperties
{
    /// <summary>
    /// The transition mode: <c>"in-out"</c>, <c>"out-in"</c>, or <c>"default"</c>/null (upstream:
    /// <c>mode</c>, https://vuejs.org/guide/built-ins/transition.html#transition-modes).
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>Whether to apply the transition on the initial render (upstream: <c>appear</c>).</summary>
    public bool Appear { get; init; }

    /// <summary>
    /// Whether the element is kept in the DOM and its visibility toggled rather than
    /// inserted/removed (upstream: <c>persisted</c>, set by <c>v-show</c>'s transition integration).
    /// A persisted transition is skipped by the renderer's mount/remove enter/leave calls.
    /// </summary>
    public bool Persisted { get; init; }

    /// <summary>Fires before the enter transition begins (upstream: <c>onBeforeEnter</c>).</summary>
    public Action<object>? OnBeforeEnter { get; init; }

    /// <summary>Runs the enter transition; must invoke its <c>done</c> callback (upstream: <c>onEnter</c>).</summary>
    public TransitionEnterHook? OnEnter { get; init; }

    /// <summary>Fires after the enter transition completes (upstream: <c>onAfterEnter</c>).</summary>
    public Action<object>? OnAfterEnter { get; init; }

    /// <summary>Fires when an enter transition is cancelled by an interrupting leave (upstream: <c>onEnterCancelled</c>).</summary>
    public Action<object>? OnEnterCancelled { get; init; }

    /// <summary>Fires before the leave transition begins (upstream: <c>onBeforeLeave</c>).</summary>
    public Action<object>? OnBeforeLeave { get; init; }

    /// <summary>Runs the leave transition; must invoke its <c>done</c> callback (upstream: <c>onLeave</c>).</summary>
    public TransitionEnterHook? OnLeave { get; init; }

    /// <summary>Fires after the leave transition completes (upstream: <c>onAfterLeave</c>).</summary>
    public Action<object>? OnAfterLeave { get; init; }

    /// <summary>Fires when a leave transition is cancelled by an interrupting enter (upstream: <c>onLeaveCancelled</c>).</summary>
    public Action<object>? OnLeaveCancelled { get; init; }

    /// <summary>Fires before the initial-render appear transition begins (upstream: <c>onBeforeAppear</c>).</summary>
    public Action<object>? OnBeforeAppear { get; init; }

    /// <summary>Runs the initial-render appear transition; must invoke its <c>done</c> callback (upstream: <c>onAppear</c>).</summary>
    public TransitionEnterHook? OnAppear { get; init; }

    /// <summary>Fires after the appear transition completes (upstream: <c>onAfterAppear</c>).</summary>
    public Action<object>? OnAfterAppear { get; init; }

    /// <summary>Fires when an appear transition is cancelled (upstream: <c>onAppearCancelled</c>).</summary>
    public Action<object>? OnAppearCancelled { get; init; }
}
