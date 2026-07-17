using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Watches a ref and invokes <paramref name="callback"/> with the new and previous values when it
    /// changes (Vue's <c>watch()</c>, https://vuejs.org/api/reactivity-core.html#watch). With
    /// <see cref="WatchOptions.Deep"/> the ref's value is traversed so a nested reactive change also
    /// fires the callback.
    /// </summary>
    /// <typeparam name="T">The watched value type.</typeparam>
    /// <param name="source">The ref to watch.</param>
    /// <param name="callback">Receives <c>(newValue, oldValue, onCleanup)</c>.</param>
    /// <param name="options">Immediate/once/deep/flush options.</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<T>(IReference<T> source, WatchCallback<T> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(callback);
        var depth = ResolveDepth(options);
        Func<T> getter = depth <= 0
            ? () => source.Value
            : () =>
            {
                var value = source.Value;
                new ReactiveTraversal(depth).Visit(value);
                return value;
            };
        return CreateWatch(getter, callback, DefaultHasChanged, depth > 0, default!, options);
    }

    /// <summary>
    /// Watches a getter and invokes <paramref name="callback"/> with the new and previous return
    /// values when any reactive value the getter reads changes (Vue's <c>watch(getter, cb)</c>).
    /// </summary>
    /// <typeparam name="T">The watched value type.</typeparam>
    /// <param name="source">The getter whose reactive reads are tracked.</param>
    /// <param name="callback">Receives <c>(newValue, oldValue, onCleanup)</c>.</param>
    /// <param name="options">Immediate/once/deep/flush options.</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<T>(Func<T> source, WatchCallback<T> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(callback);
        var depth = ResolveDepth(options);
        Func<T> getter = depth <= 0
            ? source
            : () =>
            {
                var value = source();
                new ReactiveTraversal(depth).Visit(value);
                return value;
            };
        return CreateWatch(getter, callback, DefaultHasChanged, depth > 0, default!, options);
    }

    /// <summary>
    /// Watches a source-generated reactive object; the callback fires when any of its reactive
    /// members (deep) changes (Vue's <c>watch(reactiveObject, cb)</c>, which is deep by default). The
    /// callback receives the same instance as both new and old value — the object is mutated in place
    /// (upstream parity). Set <see cref="WatchOptions.DeepDepth"/> to bound the traversal.
    /// </summary>
    /// <typeparam name="TReactive">The reactive object type.</typeparam>
    /// <param name="source">The reactive object to watch.</param>
    /// <param name="callback">Receives <c>(value, value, onCleanup)</c>.</param>
    /// <param name="options">Once/flush options (deep is implied).</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<TReactive>(TReactive source, WatchCallback<TReactive> callback, WatchOptions? options = null)
        where TReactive : class, IReactiveObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(callback);
        var depth = options?.DeepDepth ?? int.MaxValue;
        Func<TReactive> getter = () =>
        {
            new ReactiveTraversal(depth).Visit(source);
            return source;
        };
        return CreateWatch(getter, callback, NeverChanged, alwaysCallback: true, source, options);
    }

    /// <summary>
    /// Watches several refs at once; the callback receives arrays of the new and previous values with
    /// per-source old values preserved (Vue's array-source <c>watch</c>).
    /// </summary>
    /// <param name="sources">The refs to watch.</param>
    /// <param name="callback">Receives <c>(newValues, oldValues, onCleanup)</c>.</param>
    /// <param name="options">Immediate/once/deep/flush options.</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch(IReference[] sources, WatchCallback<object?[]> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var getters = new Func<object?>[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            var reference = sources[index] ?? throw new ArgumentNullException(nameof(sources));
            getters[index] = () => reference.Value;
        }
        return Watch(getters, callback, options);
    }

    /// <summary>
    /// Watches several getters at once; the callback receives arrays of the new and previous values
    /// with per-source old values preserved (Vue's array-source <c>watch</c>).
    /// </summary>
    /// <param name="sources">The getters to watch.</param>
    /// <param name="callback">Receives <c>(newValues, oldValues, onCleanup)</c>.</param>
    /// <param name="options">Immediate/once/deep/flush options.</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch(Func<object?>[] sources, WatchCallback<object?[]> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(callback);
        var count = sources.Length;
        var depth = ResolveDepth(options);
        Func<object?[]> getter = () =>
        {
            var values = new object?[count];
            for (var index = 0; index < count; index++)
            {
                values[index] = sources[index]();
            }
            if (depth > 0)
            {
                for (var index = 0; index < count; index++)
                {
                    new ReactiveTraversal(depth).Visit(values[index]);
                }
            }
            return values;
        };
        // Per-source old values start unset as an array of nulls so the immediate callback can index.
        var unset = new object?[count];
        return CreateWatch(getter, callback, ArrayHasChanged, depth > 0, unset, options);
    }

    /// <summary>
    /// Runs <paramref name="effect"/> immediately, tracking every reactive value it reads, and re-runs
    /// it whenever any of them changes (Vue's <c>watchEffect()</c>,
    /// https://vuejs.org/api/reactivity-core.html#watcheffect). The <c>onCleanup</c> argument registers
    /// a cleanup that runs before the next run and on stop.
    /// </summary>
    /// <param name="effect">The effect body; its <see cref="OnCleanup"/> argument registers a cleanup callback.</param>
    /// <param name="options">Flush options (immediate/once/deep do not apply).</param>
    /// <returns>A handle to stop or pause the effect.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effect"/> is null.</exception>
    public static WatchHandle WatchEffect(Action<OnCleanup> effect, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var watcher = new EffectWatcher(effect, options?.Flush ?? WatchFlushMode.Sync, options?.Scheduler);
        return new WatchHandle(watcher);
    }

    /// <summary>
    /// Runs <paramref name="effect"/> immediately, tracking every reactive value it reads, and re-runs
    /// it whenever any of them changes (Vue's <c>watchEffect()</c>). Overload for effects that need no
    /// cleanup registration.
    /// </summary>
    /// <param name="effect">The effect body.</param>
    /// <param name="options">Flush options.</param>
    /// <returns>A handle to stop or pause the effect.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effect"/> is null.</exception>
    public static WatchHandle WatchEffect(Action effect, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return WatchEffect(_ => effect(), options);
    }

    private static WatchHandle CreateWatch<T>(
        Func<T> getter,
        WatchCallback<T> callback,
        Func<T, T, bool> hasChanged,
        bool alwaysCallback,
        T unsetOldValue,
        WatchOptions? options)
    {
        var watcher = new Watcher<T>(
            getter,
            callback,
            hasChanged,
            alwaysCallback,
            unsetOldValue,
            options?.Immediate ?? false,
            options?.Flush ?? WatchFlushMode.Sync,
            options?.Scheduler,
            options?.Once ?? false);
        return new WatchHandle(watcher);
    }

    private static int ResolveDepth(WatchOptions? options)
    {
        if (options is null)
        {
            return 0;
        }
        if (options.DeepDepth is int depth)
        {
            return depth < 0 ? 0 : depth;
        }
        return options.Deep ? int.MaxValue : 0;
    }

    private static bool DefaultHasChanged<T>(T newValue, T oldValue)
        => !EqualityComparer<T>.Default.Equals(newValue, oldValue);

    private static bool NeverChanged<T>(T newValue, T oldValue) => false;

    private static bool ArrayHasChanged(object?[] newValues, object?[] oldValues)
    {
        if (newValues.Length != oldValues.Length)
        {
            return true;
        }
        for (var index = 0; index < newValues.Length; index++)
        {
            if (!Equals(newValues[index], oldValues[index]))
            {
                return true;
            }
        }
        return false;
    }
}
