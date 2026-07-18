namespace Assimalign.Vue.Reactivity;

/// <summary>
/// When a watcher's callback runs relative to a reactive change — the C# port of Vue 3.5's
/// <c>flush</c> option (https://vuejs.org/guide/essentials/watchers.html#callback-flush-timing).
/// <see cref="Pre"/> and <see cref="Post"/> are delivered through an injected
/// <see cref="IWatchScheduler"/> because the flush queue lives in the runtime, not the reactivity
/// layer; standalone reactivity defaults to <see cref="Sync"/>.
/// </summary>
public enum WatchFlushMode
{
    /// <summary>Run the callback synchronously the moment a dependency triggers (upstream <c>flush: 'sync'</c>).</summary>
    Sync,

    /// <summary>
    /// Queue the callback to run before the component re-renders in the same tick (upstream default
    /// <c>flush: 'pre'</c>) — delegated to the injected <see cref="IWatchScheduler"/>.
    /// </summary>
    Pre,

    /// <summary>
    /// Queue the callback to run after the component re-renders (upstream <c>flush: 'post'</c>) —
    /// delegated to the injected <see cref="IWatchScheduler"/>.
    /// </summary>
    Post,
}
