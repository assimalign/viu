using System;

namespace Assimalign.Viu;

/// <summary>
/// Configures the platform-neutral enter and leave choreography used by
/// <see cref="BaseTransition"/>.
/// </summary>
/// <remarks>
/// This is the C# counterpart of Vue 3.5's <c>BaseTransitionProps</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/BaseTransition.ts.
/// </remarks>
public sealed class BaseTransitionProperties
{
    /// <summary>Gets the transition mode: default, <c>out-in</c>, or <c>in-out</c>.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets whether the initial mount uses the appear hooks.</summary>
    public bool Appear { get; init; }

    /// <summary>Gets whether visibility is toggled without renderer-owned insertion or removal.</summary>
    public bool Persisted { get; init; }

    /// <summary>Gets the callback invoked before an enter phase.</summary>
    public Action<object>? OnBeforeEnter { get; init; }

    /// <summary>Gets the enter phase callback.</summary>
    public TransitionEnterHook? OnEnter { get; init; }

    /// <summary>Gets the callback invoked after a successful enter phase.</summary>
    public Action<object>? OnAfterEnter { get; init; }

    /// <summary>Gets the callback invoked when an enter phase is cancelled.</summary>
    public Action<object>? OnEnterCancelled { get; init; }

    /// <summary>Gets the callback invoked before a leave phase.</summary>
    public Action<object>? OnBeforeLeave { get; init; }

    /// <summary>Gets the leave phase callback.</summary>
    public TransitionEnterHook? OnLeave { get; init; }

    /// <summary>Gets the callback invoked after a successful leave phase.</summary>
    public Action<object>? OnAfterLeave { get; init; }

    /// <summary>Gets the callback invoked when a leave phase is cancelled.</summary>
    public Action<object>? OnLeaveCancelled { get; init; }

    /// <summary>Gets the callback invoked before the initial appear phase.</summary>
    public Action<object>? OnBeforeAppear { get; init; }

    /// <summary>Gets the initial appear phase callback.</summary>
    public TransitionEnterHook? OnAppear { get; init; }

    /// <summary>Gets the callback invoked after a successful appear phase.</summary>
    public Action<object>? OnAfterAppear { get; init; }

    /// <summary>Gets the callback invoked when an appear phase is cancelled.</summary>
    public Action<object>? OnAppearCancelled { get; init; }
}
