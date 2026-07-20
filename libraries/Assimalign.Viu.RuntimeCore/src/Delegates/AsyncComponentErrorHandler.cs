using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// User-controlled loader-failure policy — the C# port of the <c>onError</c> option of
/// <c>defineAsyncComponent</c> (<c>packages/runtime-core/src/apiAsyncComponent.ts</c>,
/// https://vuejs.org/guide/components/async.html#handling-errors). Called on every loader failure
/// with the error, a <paramref name="retry"/> action that re-invokes the loader as a fresh attempt,
/// a <paramref name="fail"/> action that settles the load to the error state, and the one-based
/// <paramref name="attempts"/> count made so far. Invoke exactly one of <paramref name="retry"/> or
/// <paramref name="fail"/>; invoking neither leaves the load pending.
/// </summary>
/// <param name="error">The loader failure.</param>
/// <param name="retry">Re-runs the loader; the next failure reports <paramref name="attempts"/> + 1.</param>
/// <param name="fail">Settles the load to the error state (renders the error component, if any).</param>
/// <param name="attempts">The one-based count of load attempts made so far.</param>
public delegate void AsyncComponentErrorHandler(Exception error, Action retry, Action fail, int attempts);
