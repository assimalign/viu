namespace Assimalign.Viu.Reactivity;

/// <summary>
/// When a watcher's callback runs relative to a reactive change.
/// <see cref="Pre"/> and <see cref="Post"/> are delivered through an injected
/// <see cref="IReactiveWatchScheduler"/> because the flush queue lives in the runtime, not the reactivity
/// layer; standalone reactivity defaults to <see cref="Sync"/>. Specified by <c>[RCT-12]</c>.
/// </summary>
public enum WatchFlushMode
{
    /// <summary>Run the callback synchronously the moment a dependency triggers.</summary>
    Sync,

    /// <summary>
    /// Queue the callback to run before the component re-renders in the same tick, so the callback
    /// sees pre-render state and its own writes still land in that render — delegated to the
    /// injected <see cref="IReactiveWatchScheduler"/>.
    /// </summary>
    Pre,

    /// <summary>
    /// Queue the callback to run after the component re-renders —
    /// delegated to the injected <see cref="IReactiveWatchScheduler"/>.
    /// </summary>
    Post,
}
