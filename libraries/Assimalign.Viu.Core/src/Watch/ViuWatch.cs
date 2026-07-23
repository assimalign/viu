using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// The runtime-bound watch API — the C# port of <c>watch</c>/<c>watchEffect</c> as Vue itself
/// exports them (<c>packages/runtime-core/src/apiWatch.ts</c>,
/// https://vuejs.org/api/reactivity-core.html#watch), layered over
/// <see cref="Reactive.Watch{T}(Func{T}, WatchCallback{T}, WatchOptions)"/>. Two things distinguish
/// it from the standalone reactivity watch: callbacks default to <see cref="WatchFlushMode.Pre"/>
/// timing on the runtime scheduler (batched into, and running ahead of, the render flush —
/// upstream's default), and a callback, effect-body, or getter exception routes through the
/// component <c>OnErrorCaptured</c> chain to the app-level
/// <see cref="IApplicationContext.ErrorHandler"/> instead of tearing down the flush
/// ([V01.01.03.12], issue #28). Call during <c>Setup</c> so the watcher joins the component's
/// effect scope and stops automatically on unmount. Omitted <c>options</c> mean
/// <see cref="WatchFlushMode.Pre"/>; pass <see cref="WatchOptions"/> with an explicit
/// <see cref="WatchOptions.Flush"/> to choose <see cref="WatchFlushMode.Sync"/> or
/// <see cref="WatchFlushMode.Post"/> (a pre/post option without a scheduler gets the runtime
/// scheduler injected). Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public static class ViuWatch
{
    /// <summary>Watches a reference source (upstream: <c>watch(ref, callback)</c>).</summary>
    /// <typeparam name="T">The watched value type.</typeparam>
    /// <param name="source">The reference to watch.</param>
    /// <param name="callback">Receives <c>(value, oldValue, onCleanup)</c> after a change.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<T>(IReactiveReference<T> source, WatchCallback<T> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(callback);
        var context = ComponentContext.Current;
        return Reactive.Watch(
            source,
            WrapCallback(callback, context),
            RuntimeOptions(options, context));
    }

    /// <summary>Watches a getter source (upstream: <c>watch(() =&gt; expr, callback)</c>).</summary>
    /// <typeparam name="T">The watched value type.</typeparam>
    /// <param name="source">The tracked getter.</param>
    /// <param name="callback">Receives <c>(value, oldValue, onCleanup)</c> after a change.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<T>(Func<T> source, WatchCallback<T> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(callback);
        var context = ComponentContext.Current;
        return Reactive.Watch(
            WrapGetter(source, context),
            WrapCallback(callback, context),
            RuntimeOptions(options, context));
    }

    /// <summary>
    /// Watches a source-generated reactive object, deep by default (upstream:
    /// <c>watch(reactiveObject, callback)</c>).
    /// </summary>
    /// <typeparam name="TReactive">The reactive object type.</typeparam>
    /// <param name="source">The reactive object to watch.</param>
    /// <param name="callback">Receives <c>(value, oldValue, onCleanup)</c> after a change.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch<TReactive>(TReactive source, WatchCallback<TReactive> callback, WatchOptions? options = null)
        where TReactive : class, IReactiveObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(callback);
        var context = ComponentContext.Current;
        return Reactive.Watch(
            source,
            WrapCallback(callback, context),
            RuntimeOptions(options, context));
    }

    /// <summary>Watches multiple reference sources (upstream: <c>watch([refA, refB], callback)</c>).</summary>
    /// <param name="sources">The references to watch.</param>
    /// <param name="callback">Receives the value and old-value arrays, index-aligned with <paramref name="sources"/>.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch(IReactiveReference[] sources, WatchCallback<object?[]> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(callback);
        var context = ComponentContext.Current;
        return Reactive.Watch(
            sources,
            WrapCallback(callback, context),
            RuntimeOptions(options, context));
    }

    /// <summary>Watches multiple getter sources (upstream: <c>watch([() =&gt; a, () =&gt; b], callback)</c>).</summary>
    /// <param name="sources">The tracked getters.</param>
    /// <param name="callback">Receives the value and old-value arrays, index-aligned with <paramref name="sources"/>.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or <paramref name="callback"/> is null.</exception>
    public static WatchHandle Watch(Func<object?>[] sources, WatchCallback<object?[]> callback, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(callback);
        var context = ComponentContext.Current;
        var wrappedSources = new Func<object?>[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            ArgumentNullException.ThrowIfNull(source, nameof(sources));
            wrappedSources[index] = WrapGetter(source, context);
        }
        return Reactive.Watch(
            wrappedSources,
            WrapCallback(callback, context),
            RuntimeOptions(options, context));
    }

    /// <summary>
    /// Runs <paramref name="effect"/> immediately and re-runs it when its tracked dependencies
    /// change (upstream: <c>watchEffect(effect)</c>), on pre-flush runtime timing by default.
    /// </summary>
    /// <param name="effect">The effect body; receives <c>onCleanup</c> for pre-re-run cleanup.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effect"/> is null.</exception>
    public static WatchHandle WatchEffect(Action<OnCleanup> effect, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var context = ComponentContext.Current;
        return Reactive.WatchEffect(
            WrapEffect(effect, context),
            RuntimeOptions(options, context));
    }

    /// <summary>Convenience <see cref="WatchEffect(Action{OnCleanup}, WatchOptions)"/> for an effect with no cleanup.</summary>
    /// <param name="effect">The effect body.</param>
    /// <param name="options">The watch options; null means pre-flush runtime defaults.</param>
    /// <returns>The handle that stops, pauses, or resumes the watcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="effect"/> is null.</exception>
    public static WatchHandle WatchEffect(Action effect, WatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return WatchEffect(_ => effect(), options);
    }

    private static WatchOptions RuntimeOptions(
        WatchOptions? options,
        ComponentContext? context)
    {
        ApplicationWatchScheduler scheduler =
            context?.WatchScheduler ?? new ApplicationWatchScheduler();
        if (options is null)
        {
            // Upstream default: flush 'pre' on the runtime scheduler.
            return new WatchOptions
            {
                Flush = WatchFlushMode.Pre,
                Scheduler = scheduler,
            };
        }
        if (options.Flush != WatchFlushMode.Sync && options.Scheduler is null)
        {
            // Copy rather than mutate the caller's options object.
            return new WatchOptions
            {
                Immediate = options.Immediate,
                Once = options.Once,
                Deep = options.Deep,
                DeepDepth = options.DeepDepth,
                Flush = options.Flush,
                Scheduler = scheduler,
            };
        }
        return options;
    }

    private static WatchCallback<T> WrapCallback<T>(WatchCallback<T> callback, ComponentContext? context)
        => (value, oldValue, onCleanup) =>
        {
            try
            {
                callback(value, oldValue, onCleanup);
            }
            catch (Exception exception)
            {
                // Upstream ErrorCodes.WATCH_CALLBACK: route up the captured chain to the app
                // handler; with no handler the error rethrows with its original stack.
                ComponentErrorHandling.Handle(exception, context, "watcher callback");
            }
        };

    private static Func<T> WrapGetter<T>(Func<T> getter, ComponentContext? context)
        => () =>
        {
            try
            {
                return getter();
            }
            catch (Exception exception)
            {
                // Upstream ErrorCodes.WATCH_GETTER; a handled getter error yields default so the
                // watcher stays alive, matching callWithErrorHandling returning undefined.
                ComponentErrorHandling.Handle(exception, context, "watcher getter");
                return default!;
            }
        };

    private static Action<OnCleanup> WrapEffect(Action<OnCleanup> effect, ComponentContext? context)
        => onCleanup =>
        {
            try
            {
                effect(onCleanup);
            }
            catch (Exception exception)
            {
                ComponentErrorHandling.Handle(exception, context, "watcher callback");
            }
        };
}
