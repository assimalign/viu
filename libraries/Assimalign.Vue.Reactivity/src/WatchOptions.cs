namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Options for <see cref="Reactive.Watch{T}(System.Func{T},WatchCallback{T},WatchOptions)"/> and
/// <see cref="Reactive.WatchEffect(System.Action{System.Action},WatchOptions)"/> — the C# port of
/// Vue 3.5's <c>WatchOptions</c> (https://vuejs.org/api/reactivity-core.html#watch). Deep traversal,
/// immediate/once firing, and flush timing all map to their upstream counterparts.
/// </summary>
public sealed class WatchOptions
{
    /// <summary>
    /// Fire the callback once immediately on creation with <c>oldValue</c> unset (upstream
    /// <c>immediate: true</c>). Ignored by <c>WatchEffect</c>, which always runs immediately.
    /// </summary>
    public bool Immediate { get; set; }

    /// <summary>
    /// Stop the watcher automatically after its first callback (upstream 3.4+ <c>once: true</c>). With
    /// <see cref="Immediate"/> the immediate call is that first callback.
    /// </summary>
    public bool Once { get; set; }

    /// <summary>
    /// Traverse the source so a change to any nested reactive member fires the callback (upstream
    /// <c>deep: true</c>). Applies to source-generated <see cref="IReactiveObject"/> values and
    /// reactive collections; plain CLR objects are not traversable. Overridden by
    /// <see cref="DeepDepth"/> when that is set. A source that is itself an <see cref="IReactiveObject"/>
    /// is deep by default.
    /// </summary>
    public bool Deep { get; set; }

    /// <summary>
    /// A finite traversal depth (upstream 3.5 <c>deep: number</c>). When set it takes precedence over
    /// <see cref="Deep"/>; <c>0</c> disables traversal and a positive value bounds it. Leave
    /// <see langword="null"/> to use <see cref="Deep"/>.
    /// </summary>
    public int? DeepDepth { get; set; }

    /// <summary>
    /// When the callback runs relative to the change (upstream <c>flush</c>). Defaults to
    /// <see cref="WatchFlushMode.Sync"/> in the standalone reactivity layer; the runtime sets
    /// <see cref="WatchFlushMode.Pre"/> and supplies a <see cref="Scheduler"/>.
    /// </summary>
    public WatchFlushMode Flush { get; set; } = WatchFlushMode.Sync;

    /// <summary>
    /// The scheduler that delivers <see cref="WatchFlushMode.Pre"/>/<see cref="WatchFlushMode.Post"/>
    /// callbacks. Required for those modes; ignored for <see cref="WatchFlushMode.Sync"/>. When a
    /// pre/post watcher has no scheduler it falls back to synchronous delivery.
    /// </summary>
    public IWatchScheduler? Scheduler { get; set; }
}
