using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The static entry-point facade for Viu's reactivity system: references, computeds, effects,
/// effect scopes, watches, tracking control, and batching. This is the whole discoverable surface
/// — the ratified member list is <c>[RCT-5]</c>. One application graph remains single-event-loop
/// and not thread-safe; ServerRenderer selects execution-flow-local ambient bookkeeping so distinct
/// request-owned graphs cannot share tracking, batching, or scope state. Specified by
/// <c>[EXE-1]</c>.
/// </summary>
public static class Reactive
{
    /// <summary>
    /// Creates a tracked reference cell holding <paramref name="value"/>: reads inside an effect
    /// subscribe to it, and a write that changes the value notifies those subscribers.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <returns>The new reference.</returns>
    public static Reference<T> Reference<T>(T value) => new(value);

    /// <summary>
    /// Creates a reference cell that notifies only on assignment of a new instance, never on
    /// mutation of the instance it holds — the escape hatch for large or externally owned objects
    /// whose interiors must not be tracked.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <returns>The new shallow reference.</returns>
    public static ShallowReference<T> ShallowReference<T>(T value) => new(value);

    /// <summary>
    /// Creates a reference cell whose tracking and notification the caller drives explicitly — the
    /// sanctioned extension point for behavior the built-in cells do not cover (debouncing,
    /// external stores, values that change outside a write).
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="factory">Receives track/trigger delegates and returns the getter/setter pair.</param>
    /// <returns>The new custom reference.</returns>
    public static CustomReference<T> CustomReference<T>(CustomReferenceFactory<T> factory) => new(factory);

    /// <summary>
    /// Creates a lazily evaluated, version-cached derivation over <paramref name="getter"/>: the
    /// getter is not invoked until the first read and is re-evaluated only when a source it read
    /// has actually changed. Pass <paramref name="setter"/> for the writable variant.
    /// </summary>
    /// <typeparam name="T">The computed value type.</typeparam>
    /// <param name="getter">The derivation function (not invoked until the first read).</param>
    /// <param name="setter">Optional setter making the computed writable.</param>
    /// <returns>The new computed.</returns>
    public static Computed<T> Computed<T>(Func<T> getter, Action<T>? setter = null) => new(getter, setter);

    /// <summary>
    /// Creates a reactive effect over <paramref name="action"/>, runs it immediately, and returns
    /// the runner handle. With a <paramref name="scheduler"/>, later
    /// invalidations invoke the scheduler instead of re-running the effect. If the first run
    /// throws, the effect is stopped before the exception propagates — deliberately, so a failed
    /// effect leaves no live subscriptions behind rather than a half-tracked one.
    /// </summary>
    /// <param name="action">The reactive function to track.</param>
    /// <param name="scheduler">Optional scheduler invoked on invalidation instead of a re-run.</param>
    /// <returns>
    /// The effect handle. Use <see cref="ReactiveEffect.Run"/> to execute explicitly and
    /// <see cref="ReactiveEffect.Stop"/> or <see cref="ReactiveEffect.Dispose"/> for idempotent teardown.
    /// </returns>
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

    /// <summary>
    /// Creates an effect scope — a lifetime boundary that owns the effects created inside it, so
    /// stopping the scope stops them all at once.
    /// </summary>
    /// <param name="detached">When true, the scope does not attach to the current scope.</param>
    /// <returns>The new scope.</returns>
    public static EffectScope EffectScope(bool detached = false) => new(detached);

    /// <summary>The ambient scope new effects register with, or null when none is active.</summary>
    /// <remarks>
    /// This is the single public scope accessor so construction and inspection remain discoverable
    /// through the sanctioned reactivity facade. Specified by <c>[RCT-5]</c>.
    /// </remarks>
    public static EffectScope? CurrentScope => global::Assimalign.Viu.Reactivity.EffectScope.Current;

    /// <summary>
    /// Registers a callback to run exactly once when the current scope stops. With no active
    /// scope this is a no-op that emits a debug warning unless <paramref name="failSilently"/> is
    /// set.
    /// </summary>
    /// <param name="callback">The cleanup callback.</param>
    /// <param name="failSilently">Suppresses the no-active-scope debug warning.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public static void OnScopeDispose(Action callback, bool failSilently = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var scope = global::Assimalign.Viu.Reactivity.EffectScope.Current;
        if (scope is not null && scope.IsActive)
        {
            scope.RegisterCleanup(callback);
        }
        else if (!failSilently)
        {
            Debug.WriteLine("[Viu warn] OnScopeDispose() is called when there is no active effect scope to be associated with.");
        }
    }

    /// <summary>
    /// Force-notifies a reference's subscribers regardless of value equality — used after in-place
    /// mutation of a <see cref="ShallowReference{T}"/>'s inner object, which by definition produced
    /// no notification of its own. Anything that owns a dependency is triggered, deliberately
    /// including <see cref="Computed{T}"/>: effects reading the computed re-run even though its
    /// cached value is unchanged, because a forced trigger asserts that the value's meaning
    /// changed even when its identity did not.
    /// </summary>
    /// <param name="reference">The reference to force-trigger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static void TriggerReference(IReactiveTrackedReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // A tracked reference always exposes the dependency that owns its subscribers.
        reference.Dependency.Trigger();
    }

    /// <summary>
    /// Suspends dependency collection on the current thread; pair with <see cref="ResetTracking"/>.
    /// Reads taken while paused do not subscribe.
    /// </summary>
    public static void PauseTracking() => ReactivityState.PauseTracking();

    /// <summary>Restores the tracking state saved by the matching <see cref="PauseTracking"/>.</summary>
    public static void ResetTracking() => ReactivityState.ResetTracking();

    /// <summary>
    /// Opens a batch whose queued triggers are coalesced until the returned scope is disposed.
    /// Nested scopes flush only when the outermost scope is disposed.
    /// </summary>
    /// <returns>
    /// An idempotent scope that closes exactly one batch level. The disposable shape ensures that a
    /// <c>using</c> statement restores effect delivery when its body exits through an exception.
    /// </returns>
    /// <remarks>Specified by <c>[RCT-5]</c>.</remarks>
    public static IDisposable Batch() => new BatchScope();

    /// <summary>
    /// Watches a reference and invokes <paramref name="callback"/> with the new and previous values
    /// when it changes. With <see cref="WatchOptions.Deep"/> the value is traversed on each read so
    /// a nested reactive change also fires the callback.
    /// </summary>
    /// <typeparam name="T">The watched value type.</typeparam>
    /// <param name="source">The reference to watch.</param>
    /// <param name="callback">Receives <c>(newValue, oldValue, onCleanup)</c>.</param>
    /// <param name="options">Immediate/once/deep/flush options.</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<T>(
        IReactiveReference<T> source,
        WatchCallback<T> callback,
        WatchOptions? options = null)
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
    /// values when any reactive value the getter reads changes. The getter's reads define the
    /// dependency set, so it is re-tracked on every run.
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
    /// members changes, at any depth (this overload is deep by definition). The callback receives
    /// the same instance as both new and old value: the object is mutated in place, so there is no
    /// prior snapshot to hand back and none is fabricated. Set
    /// <see cref="WatchOptions.DeepDepth"/> to bound the traversal.
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
    /// Watches several references at once; the callback receives arrays of the new and previous
    /// values, with each source's own previous value preserved at its index.
    /// </summary>
    /// <param name="sources">The references to watch.</param>
    /// <param name="callback">Receives <c>(newValues, oldValues, onCleanup)</c>.</param>
    /// <param name="options">Immediate/once/deep/flush options.</param>
    /// <returns>A handle to stop or pause the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch(
        IReactiveReference[] sources,
        WatchCallback<object?[]> callback,
        WatchOptions? options = null)
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
    /// Watches several getters at once; the callback receives arrays of the new and previous
    /// values, with each source's own previous value preserved at its index.
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
    /// Runs <paramref name="effect"/> immediately, tracking every reactive value it reads, and
    /// re-runs it whenever any of them changes. There is no separate source declaration: the body
    /// itself is the dependency set, re-tracked on every run. The <c>onCleanup</c> argument
    /// registers a cleanup that runs before the next run and on stop.
    /// </summary>
    /// <param name="effect">The effect body; its <see cref="OnCleanup"/> argument registers a cleanup callback.</param>
    /// <param name="options">Flush options (immediate/once/deep do not apply).</param>
    /// <returns>A handle to stop or pause the effect.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effect"/> is null.</exception>
    public static WatchHandle WatchEffect(Action<OnCleanup> effect, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var watcher = new EffectWatcher(effect, options?.Flush ?? WatchFlushMode.Synchronous, options?.Scheduler);
        return new WatchHandle(watcher);
    }

    /// <summary>
    /// Runs <paramref name="effect"/> immediately, tracking every reactive value it reads, and
    /// re-runs it whenever any of them changes. Overload for effects that need no cleanup
    /// registration.
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

    // ---- Reactivity utilities and escape hatches ----
    // Inspection ("is this reactive?") and the deliberate ways out of reactivity. All introspection
    // is interface/type-check based so it stays O(1), reflection-free, and trim/AOT-safe.
    // Deviates from the repository whole-word naming rule per a ratified API-shape decision:
    // IsRef, Unref, and ToRef are frozen short forms, because the whole-word spellings collide with
    // the surface they operate on -- ToReference is indistinguishable at a call site from the
    // Reference<T> factory, and Unreference reads as releasing a reference rather than reading
    // through one. The contracts and concrete types still spell out Reference. See
    // docs/DESIGN.md, "The ratified public surface".

    /// <summary>
    /// Whether <paramref name="value"/> is a reference: any <see cref="IReactiveReference"/> — a plain,
    /// shallow, custom, or computed reference, or a
    /// <see cref="ToRef{T}(Func{T}, Action{T})"/>/<c>ToReferences()</c> projection.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a reference.</returns>
    public static bool IsRef(object? value) => value is IReactiveReference;

    /// <summary>
    /// Whether <paramref name="value"/> is a reactive object — a source-generated
    /// <c>[Reactive]</c>/<c>[ShallowReactive]</c> object or a reactive collection
    /// (<see cref="ReactiveList{T}"/>/<see cref="ReactiveDictionary{TKey,TValue}"/>/<see cref="ReactiveSet{T}"/>),
    /// and not one excluded by <see cref="MarkRaw{T}"/>. References and computeds are not reactive
    /// objects (use <see cref="IsRef"/>). The check keys on <see cref="IReactiveTraversable"/>, which in
    /// Viu is implemented by exactly those two families — which is what keeps it O(1) and
    /// reflection-free.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a (non-marked) reactive object.</returns>
    public static bool IsReactive(object? value)
        => value is IReactiveTraversable traversable && !RawMarkers.IsMarked(traversable);

    /// <summary>
    /// Whether <paramref name="value"/> is a read-only reactive view — a getter-only
    /// <see cref="Computed{T}"/> or a source-generated <c>[Reactive(ReadOnly = true)]</c>/
    /// <c>[ShallowReactive(ReadOnly = true)]</c> object. Keys on
    /// <see cref="IReactiveReadOnly.IsReadOnly"/> for references and generated reactive objects.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> rejects writes.</returns>
    public static bool IsReadOnly(object? value)
        => value is IReactiveReadOnly reactiveReadOnly && reactiveReadOnly.IsReadOnly;

    /// <summary>
    /// Returns the value inside a reference, or the argument itself when it is not a reference, so a
    /// caller can accept "value or reference" without branching. This overload unwraps any
    /// <see cref="IReactiveReference{T}"/> without boxing <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="reference">The reference to unwrap.</param>
    /// <returns>The reference's current value (a tracked read).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static T Unref<T>(ReactiveValue<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <summary>
    /// Returns the current value of an interface-facing reactive reference. Keeping this overload
    /// alongside the engine-base overload lets consumers remain independent of first-party
    /// implementation types.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="reference">The reference to unwrap.</param>
    /// <returns>The reference's current value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static T Unref<T>(IReactiveReference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(IReactiveReference{T})"/>
    public static T Unref<T>(Reference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(IReactiveReference{T})"/>
    public static T Unref<T>(ShallowReference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(IReactiveReference{T})"/>
    public static T Unref<T>(CustomReference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(IReactiveReference{T})"/>
    public static T Unref<T>(Computed<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <summary>
    /// Returns <paramref name="value"/> unchanged — the non-reference branch. The concrete-reference
    /// overloads take precedence for reference arguments, so a struct value flows through this overload
    /// without boxing. (A reference whose static type is only <see cref="object"/> is opaque to overload
    /// resolution and is returned as-is; unwrap it through
    /// <see cref="Unref{T}(IReactiveReference{T})"/> instead.)
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to pass through.</param>
    /// <returns><paramref name="value"/> itself.</returns>
    public static T Unref<T>(T value) => value;

    /// <summary>
    /// Creates a reference projected through <paramref name="getter"/> (and optional
    /// <paramref name="setter"/>). Reading the reference invokes the getter, so it tracks whatever
    /// reactive source the getter reads; writing routes through the setter, so it triggers that
    /// source. With no setter the reference is read-only and a write is a warned no-op. There is
    /// deliberately no property-name-string form: resolving a name to a member at runtime needs
    /// reflection, which the AOT/trimming constraint forbids. A per-property write-through
    /// reference comes from a generated object's <c>ToReferences()</c> instead.
    /// </summary>
    /// <typeparam name="T">The projected value type.</typeparam>
    /// <param name="getter">Invoked on read; its reactive reads become the reference's dependencies.</param>
    /// <param name="setter">Invoked on write, or <see langword="null"/> for a read-only reference.</param>
    /// <returns>A reference backed by the delegates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getter"/> is null.</exception>
    public static ReactiveValue<T> ToRef<T>(Func<T> getter, Action<T>? setter = null)
    {
        ArgumentNullException.ThrowIfNull(getter);
        return new AccessorReference<T>(getter, setter);
    }

    /// <summary>
    /// Returns the untracked underlying <see cref="List{T}"/> of <paramref name="list"/>. It is the
    /// same live storage, so reads off it do not track and writes through it do not trigger (an
    /// effect reading the reactive list does not re-run).
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The reactive list.</param>
    /// <returns>The underlying storage list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is null.</exception>
    public static List<T> ToRaw<T>(ReactiveList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return list.RawStorage;
    }

    /// <summary>
    /// Returns the untracked underlying <see cref="Dictionary{TKey,TValue}"/> of
    /// <paramref name="dictionary"/>. It is the same live storage, so reads off it do not track and
    /// writes through it do not trigger.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The reactive dictionary.</param>
    /// <returns>The underlying storage dictionary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is null.</exception>
    public static Dictionary<TKey, TValue> ToRaw<TKey, TValue>(ReactiveDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.RawStorage;
    }

    /// <summary>
    /// Returns the untracked underlying <see cref="HashSet{T}"/> of <paramref name="set"/>. It is the
    /// same live storage, so reads off it do not track and writes through it do not trigger.
    /// </summary>
    /// <typeparam name="T">The member type.</typeparam>
    /// <param name="set">The reactive set.</param>
    /// <returns>The underlying storage set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is null.</exception>
    public static HashSet<T> ToRaw<T>(ReactiveSet<T> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.RawStorage;
    }

    /// <summary>
    /// Marks <paramref name="value"/> so it is permanently excluded from reactivity. A marked object is
    /// skipped by deep-watch traversal (a change inside it never re-runs a deep watcher) and is reported as
    /// non-reactive by <see cref="IsReactive"/>, even when it is itself a generated
    /// <see cref="IReactiveObject"/>. There is no wrapper to strip (Viu objects are their own raw);
    /// marking is by reference identity and never keeps the object alive. Returns the same instance so
    /// calls can be chained.
    /// </summary>
    /// <typeparam name="T">The reference type of the marked value.</typeparam>
    /// <param name="value">The object to exclude from reactivity.</param>
    /// <returns><paramref name="value"/> itself.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static T MarkRaw<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        RawMarkers.Mark(value);
        return value;
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
            options?.Flush ?? WatchFlushMode.Synchronous,
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
