using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Assimalign.Viu;

/// <summary>
/// The static entry-point facade for Viu's reactivity system, mirroring the public API of
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
    public static EffectScope? CurrentScope => global::Assimalign.Viu.EffectScope.Current;

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
        var scope = global::Assimalign.Viu.EffectScope.Current;
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
    public static void TriggerReference(ReactiveValue reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // Every ReactiveValue owns a dependency, so there is no silent no-op branch: a non-null ref
        // always force-notifies its subscribers (a projected ref's own dependency simply has none).
        reference.Dependency.Trigger();
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
    public static WatchHandle Watch<T>(ReactiveValue<T> source, WatchCallback<T> callback, WatchOptions? options = null)
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
    public static WatchHandle Watch(ReactiveValue[] sources, WatchCallback<object?[]> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var getters = new Func<object?>[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            var reference = sources[index] ?? throw new ArgumentNullException(nameof(sources));
            getters[index] = () => reference.BoxedValue;
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

    // ---- Reactivity utilities and escape hatches ----
    // The C# port of @vue/reactivity's utility surface (https://vuejs.org/api/reactivity-utilities.html)
    // and advanced escape hatches (https://vuejs.org/api/reactivity-advanced.html). All introspection is
    // interface/type-check based so it stays O(1), reflection-free, and trim/AOT-safe.

    /// <summary>
    /// Whether <paramref name="value"/> is a ref (any <see cref="ReactiveValue"/>: a plain, shallow, custom,
    /// or computed ref, or a <see cref="ToRef{T}(Func{T}, Action{T})"/>/<c>ToReferences()</c> projection)
    /// — the C# port of Vue 3.5's <c>isRef()</c> (https://vuejs.org/api/reactivity-utilities.html#isref).
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a ref.</returns>
    public static bool IsRef(object? value) => value is ReactiveValue;

    /// <summary>
    /// Whether <paramref name="value"/> is a reactive object — a source-generated
    /// <c>[Reactive]</c>/<c>[ShallowReactive]</c> object or a reactive collection
    /// (<see cref="ReactiveList{T}"/>/<see cref="ReactiveDictionary{TKey,TValue}"/>/<see cref="ReactiveSet{T}"/>),
    /// and not one excluded by <see cref="MarkRaw{T}"/> — the C# port of Vue 3.5's <c>isReactive()</c>
    /// (https://vuejs.org/api/reactivity-utilities.html#isreactive). Refs and computeds are not reactive
    /// objects (use <see cref="IsRef"/>). The check keys on <see cref="IReactiveTraversable"/>, which in
    /// Viu is implemented by exactly those two families.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a (non-marked) reactive object.</returns>
    public static bool IsReactive(object? value)
        => value is IReactiveTraversable traversable && !RawMarkers.IsMarked(traversable);

    /// <summary>
    /// Whether <paramref name="value"/> is a read-only reactive view — a getter-only
    /// <see cref="Computed{T}"/> or a source-generated <c>[Reactive(Readonly = true)]</c>/
    /// <c>[ShallowReactive(Readonly = true)]</c> object — the C# port of Vue 3.5's <c>isReadonly()</c>
    /// (https://vuejs.org/api/reactivity-utilities.html#isreadonly). Keys on
    /// <see cref="ReactiveValue.IsReadOnly"/> for refs/computeds and <see cref="IReactiveObject.IsReadOnly"/>
    /// for reactive objects.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> rejects writes.</returns>
    public static bool IsReadonly(object? value) => value switch
    {
        ReactiveValue reactiveValue => reactiveValue.IsReadOnly,
        IReactiveObject reactiveObject => reactiveObject.IsReadOnly,
        _ => false,
    };

    /// <summary>
    /// Returns the value inside a ref, or the argument itself when it is not a ref — the C# port of Vue
    /// 3.5's <c>unref()</c> (https://vuejs.org/api/reactivity-utilities.html#unref). This overload unwraps
    /// any <see cref="ReactiveValue{T}"/> without boxing <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="reference">The ref to unwrap.</param>
    /// <returns>The ref's current value (a tracked read).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static T Unref<T>(ReactiveValue<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(ReactiveValue{T})"/>
    public static T Unref<T>(Reference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(ReactiveValue{T})"/>
    public static T Unref<T>(ShallowReference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(ReactiveValue{T})"/>
    public static T Unref<T>(CustomReference<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <inheritdoc cref="Unref{T}(ReactiveValue{T})"/>
    public static T Unref<T>(Computed<T> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Value;
    }

    /// <summary>
    /// Returns <paramref name="value"/> unchanged — the non-ref branch of <c>unref()</c>. The concrete-ref
    /// overloads take precedence for ref arguments, so a struct value flows through this overload without
    /// boxing. (A ref whose static type is only <see cref="object"/> is opaque to overload resolution and
    /// is returned as-is; unwrap it through <see cref="Unref{T}(ReactiveValue{T})"/> instead.)
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to pass through.</param>
    /// <returns><paramref name="value"/> itself.</returns>
    public static T Unref<T>(T value) => value;

    /// <summary>
    /// Creates a ref projected through <paramref name="getter"/> (and optional <paramref name="setter"/>)
    /// — the delegate-based form of Vue 3.5's <c>toRef()</c>
    /// (https://vuejs.org/api/reactivity-utilities.html#toref). Reading the ref invokes the getter, so it
    /// tracks whatever reactive source the getter reads; writing routes through the setter, so it triggers
    /// that source. With no setter the ref is read-only (a write is a warned no-op), mirroring a
    /// getter-only <c>toRef</c>. Property-name-string forms are intentionally omitted (AOT/trim: no
    /// reflection) — a per-property write-through ref instead comes from a generated object's
    /// <c>ToReferences()</c> (the <c>toRefs</c> counterpart).
    /// </summary>
    /// <typeparam name="T">The projected value type.</typeparam>
    /// <param name="getter">Invoked on read; its reactive reads become the ref's dependencies.</param>
    /// <param name="setter">Invoked on write, or <see langword="null"/> for a read-only ref.</param>
    /// <returns>A ref backed by the delegates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getter"/> is null.</exception>
    public static ReactiveValue<T> ToRef<T>(Func<T> getter, Action<T>? setter = null)
    {
        ArgumentNullException.ThrowIfNull(getter);
        return new AccessorReference<T>(getter, setter);
    }

    /// <summary>
    /// Returns the raw, non-reactive view of <paramref name="value"/> — the C# port of Vue 3.5's
    /// <c>toRaw()</c> (https://vuejs.org/api/reactivity-advanced.html#toraw). Viu has no identity-swapping
    /// proxy, so a source-generated <c>[Reactive]</c> object (or any non-collection value) is its own raw
    /// and is returned by identity — reads through the returned instance still track, because it <em>is</em>
    /// the reactive instance (documented C# divergence from <c>toRaw</c>, which strips a proxy). For an
    /// untracked view whose reads do not track and writes do not trigger, use the reactive-collection
    /// overloads (which return the underlying storage) or a generated object's <c>ToRawValues()</c>
    /// view (emitted per <c>[Reactive]</c> class straight over the raw backing fields).
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to unwrap.</param>
    /// <returns><paramref name="value"/> itself.</returns>
    public static T ToRaw<T>(T value) => value;

    /// <summary>
    /// Returns the untracked underlying <see cref="List{T}"/> of <paramref name="list"/> — Vue's
    /// <c>toRaw</c> on a reactive array. It is the same live storage, so reads off it do not track and
    /// writes through it do not trigger (an effect reading the reactive list does not re-run).
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
    /// Returns the untracked underlying <see cref="Dictionary{TKey,TValue}"/> of <paramref name="dictionary"/>
    /// — Vue's <c>toRaw</c> on a reactive Map. It is the same live storage, so reads off it do not track
    /// and writes through it do not trigger.
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
    /// Returns the untracked underlying <see cref="HashSet{T}"/> of <paramref name="set"/> — Vue's
    /// <c>toRaw</c> on a reactive Set. It is the same live storage, so reads off it do not track and
    /// writes through it do not trigger.
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
    /// Marks <paramref name="value"/> so it is permanently excluded from reactivity — the C# port of Vue
    /// 3.5's <c>markRaw()</c> (https://vuejs.org/api/reactivity-advanced.html#markraw). A marked object is
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
