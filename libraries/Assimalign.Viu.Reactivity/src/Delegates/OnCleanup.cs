using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The cleanup-registration function passed to a <see cref="WatchCallback{T}"/> and to a
/// <c>WatchEffect</c> body — the C# port of Vue 3.5's <c>OnCleanup</c> type
/// (<c>(cleanupFn) =&gt; void</c>, https://vuejs.org/guide/essentials/watchers.html#side-effect-cleanup).
/// Call it with a <paramref name="cleanup"/> action to register work that runs immediately before the
/// next callback/effect run and again when the watcher stops — the canonical way to cancel a stale
/// asynchronous request.
/// </summary>
/// <param name="cleanup">The cleanup action to register.</param>
public delegate void OnCleanup(Action cleanup);

