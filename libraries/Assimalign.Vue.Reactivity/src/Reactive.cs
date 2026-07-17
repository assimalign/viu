using System;
using System.Diagnostics;

namespace Assimalign.Vue.Reactivity;

/// <summary>
/// The static entry-point facade for the Vuecs reactivity system, mirroring the public API of
/// <c>@vue/reactivity</c>: refs, computeds, effects, effect scopes, tracking control, and
/// batching. All ambient state is static and NOT thread-safe by design — the runtime targets the
/// single-threaded JS event-loop model (browser WASM).
/// </summary>
public static class Reactive
{
    /// <summary>Creates a reactive ref holding <paramref name="value"/> (Vue's <c>ref()</c>).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <returns>The new ref.</returns>
    public static Reference<T> Reference<T>(T value) => new(value);

    /// <summary>Creates a shallow ref holding <paramref name="value"/> (Vue's <c>shallowRef()</c>).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <returns>The new shallow ref.</returns>
    public static ShallowReference<T> ShallowReference<T>(T value) => new(value);

    /// <summary>Creates a custom ref with explicit track/trigger control (Vue's <c>customRef()</c>).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="factory">Receives track/trigger delegates and returns the getter/setter pair.</param>
    /// <returns>The new custom ref.</returns>
    public static CustomReference<T> CustomReference<T>(CustomReferenceFactory<T> factory) => new(factory);

    /// <summary>
    /// Creates a lazy cached computed over <paramref name="getter"/>; pass <paramref name="setter"/>
    /// for the writable variant (Vue's <c>computed()</c>).
    /// </summary>
    /// <typeparam name="T">The computed value type.</typeparam>
    /// <param name="getter">The derivation function (not invoked until the first read).</param>
    /// <param name="setter">Optional setter making the computed writable.</param>
    /// <returns>The new computed.</returns>
    public static Computed<T> Computed<T>(Func<T> getter, Action<T>? setter = null) => new(getter, setter);

    /// <summary>
    /// Creates a reactive effect over <paramref name="action"/>, runs it immediately, and returns
    /// the runner handle (Vue's <c>effect()</c>). With a <paramref name="scheduler"/>, later
    /// invalidations invoke the scheduler instead of re-running the effect. If the first run
    /// throws, the effect is stopped before the exception propagates (upstream <c>effect()</c>
    /// parity), so a failed effect leaves no live subscriptions behind.
    /// </summary>
    /// <param name="action">The reactive function to track.</param>
    /// <param name="scheduler">Optional scheduler invoked on invalidation instead of a re-run.</param>
    /// <returns>The effect handle (use <see cref="ReactiveEffect.Run"/>/<see cref="ReactiveEffect.Stop"/>).</returns>
    public static ReactiveEffect Effect(Action action, Action? scheduler = null)
    {
        var effect = new ReactiveEffect(action) { Scheduler = scheduler };
        try
        {
            effect.Run();
        }
        catch
        {
            effect.Stop();
            throw;
        }
        return effect;
    }

    /// <summary>Creates an effect scope (Vue's <c>effectScope()</c>).</summary>
    /// <param name="detached">When true, the scope does not attach to the current scope.</param>
    /// <returns>The new scope.</returns>
    public static EffectScope EffectScope(bool detached = false) => new(detached);

    /// <summary>The ambient scope new effects and computeds register with (Vue's <c>getCurrentScope()</c>).</summary>
    public static EffectScope? CurrentScope => Reactivity.EffectScope.Current;

    /// <summary>
    /// Registers a callback to run exactly once when the current scope stops (Vue's
    /// <c>onScopeDispose()</c>). With no active scope this is a no-op that emits a debug warning
    /// unless <paramref name="failSilently"/> is set.
    /// </summary>
    /// <param name="callback">The cleanup callback.</param>
    /// <param name="failSilently">Suppresses the no-active-scope debug warning.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public static void OnScopeDispose(Action callback, bool failSilently = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var scope = Reactivity.EffectScope.Current;
        if (scope is not null && scope.IsActive)
        {
            scope.RegisterCleanup(callback);
        }
        else if (!failSilently)
        {
            Debug.WriteLine("[Vue warn] OnScopeDispose() is called when there is no active effect scope to be associated with.");
        }
    }

    /// <summary>
    /// Force-notifies a ref's subscribers regardless of value equality — used after in-place
    /// mutation of a <see cref="ShallowReference{T}"/>'s inner object (Vue's <c>triggerRef()</c>).
    /// Anything that owns a dependency is triggered, including <see cref="Computed{T}"/> (upstream
    /// parity): effects reading the computed re-run even though its cached value is unchanged.
    /// </summary>
    /// <param name="reference">The ref to force-trigger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static void TriggerReference(IReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference is ITrackedReference tracked)
        {
            tracked.Dependency.Trigger();
        }
    }

    /// <summary>Pauses dependency tracking (Vue's <c>pauseTracking()</c>); pair with <see cref="ResetTracking"/>.</summary>
    public static void PauseTracking() => ReactivityState.PauseTracking();

    /// <summary>Restores the tracking state saved by the matching <see cref="PauseTracking"/>.</summary>
    public static void ResetTracking() => ReactivityState.ResetTracking();

    /// <summary>
    /// Opens a batch: triggers are queued and coalesced until the matching <see cref="EndBatch"/>,
    /// so multiple writes produce at most one run per effect.
    /// </summary>
    public static void StartBatch() => ReactivityState.StartBatch();

    /// <summary>Closes the innermost batch, flushing queued effects when it is the outermost one.</summary>
    /// <exception cref="InvalidOperationException">There is no open batch to close.</exception>
    public static void EndBatch() => ReactivityState.EndBatch();
}
