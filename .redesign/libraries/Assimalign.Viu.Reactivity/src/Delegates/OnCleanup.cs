using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The cleanup-registration function passed to a <see cref="WatchCallback{T}"/> and to a
/// <c>WatchEffect</c> body. The registered callback runs before the next invocation and once more
/// when the watcher stops, so an in-flight asynchronous operation can be cancelled exactly once.
/// Call it with a <paramref name="cleanup"/> action to register work that runs immediately before the
/// next callback/effect run and again when the watcher stops — the canonical way to cancel a stale
/// asynchronous request. Specified by <c>[RCT-5]</c>.
/// </summary>
/// <param name="cleanup">The cleanup action to register.</param>
public delegate void OnCleanup(Action cleanup);

