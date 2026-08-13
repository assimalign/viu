using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// Carries resolved host transition behavior through a transition invocation.
/// </summary>
/// <remarks>
/// Core consumes only these host-neutral callbacks. A host package remains responsible for class
/// names, scheduling, layout, and end detection. The same instance may be shared by transition
/// children, but its callbacks are invoked only on the renderer's single-threaded host loop.
/// Specified by <c>[BLT-7]</c> through <c>[BLT-10]</c>.
/// </remarks>
public sealed class TransitionProperties
{
    /// <summary>
    /// Gets the reserved invocation argument that carries resolved transition properties.
    /// </summary>
    public const string ResolvedArgument = "$transition";

    internal const string StateArgument = "$transitionState";

    /// <summary>Gets the mode: default, <c>out-in</c>, or <c>in-out</c>.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets whether the initial client mount runs the appear phase.</summary>
    public bool Appear { get; init; }

    /// <summary>Gets whether a directive, rather than the renderer, drives visibility changes.</summary>
    public bool Persisted { get; init; }

    /// <summary>Gets the callback invoked before an enter phase.</summary>
    public Action<object>? OnBeforeEnter { get; init; }

    /// <summary>Gets the callback that runs an enter phase.</summary>
    public TransitionPhaseHook? OnEnter { get; init; }

    /// <summary>Gets the callback invoked after a successful enter phase.</summary>
    public Action<object>? OnAfterEnter { get; init; }

    /// <summary>Gets the callback invoked when an enter phase is cancelled.</summary>
    public Action<object>? OnEnterCancelled { get; init; }

    /// <summary>Gets the callback invoked before a leave phase.</summary>
    public Action<object>? OnBeforeLeave { get; init; }

    /// <summary>Gets the callback that runs a leave phase.</summary>
    public TransitionPhaseHook? OnLeave { get; init; }

    /// <summary>Gets the callback invoked after a successful leave phase.</summary>
    public Action<object>? OnAfterLeave { get; init; }

    /// <summary>Gets the callback invoked when a leave phase is cancelled.</summary>
    public Action<object>? OnLeaveCancelled { get; init; }

    /// <summary>Gets the callback invoked before an initial appear phase.</summary>
    public Action<object>? OnBeforeAppear { get; init; }

    /// <summary>Gets the callback that runs an initial appear phase.</summary>
    public TransitionPhaseHook? OnAppear { get; init; }

    /// <summary>Gets the callback invoked after a successful appear phase.</summary>
    public Action<object>? OnAfterAppear { get; init; }

    /// <summary>Gets the callback invoked when an appear phase is cancelled.</summary>
    public Action<object>? OnAppearCancelled { get; init; }

    /// <summary>Gets the observer that receives the outgoing keyed-element snapshot.</summary>
    public Action<IReadOnlyList<TransitionElementSnapshot>>? OnBeforeUpdate { get; init; }

    /// <summary>Gets the observer that receives the patched incoming keyed-element snapshot.</summary>
    public Action<IReadOnlyList<TransitionElementSnapshot>>? OnUpdated { get; init; }
}
