using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// The delay/timeout timer seam async components schedule through — the C# stand-in for the two
/// <c>setTimeout</c> calls in upstream's <c>apiAsyncComponent.ts</c> (the <c>delay</c> before the
/// loading component and the <c>timeout</c> before the error state). Vue has no scheduler-level timer
/// abstraction to reuse (the <see cref="Scheduler"/> is a microtask queue, not a macrotask timer), so
/// this is the injected clock/delay seam the ticket ([V01.01.03.16]) calls for.
/// <para>
/// The default schedules a real <see cref="Task.Delay(int, CancellationToken)"/> whose continuation
/// resumes on the captured single-threaded synchronization context (the WASM main thread) — never
/// off-context. Tests replace <see cref="Scheduler"/> with a manual controller so time is
/// deterministic: no wall-clock waits, no ambient timer thread, and "the loading component appears
/// only after the delay" is pinned by advancing a fake clock. Disposing the returned handle cancels a
/// not-yet-fired timer, which is how an async component discards its pending timers on unmount.
/// Ambient static, single-threaded — NOT thread-safe.
/// </para>
/// </summary>
internal static class AsyncComponentDelay
{
    /// <summary>
    /// Test seam: when set, timers are scheduled through this dispatcher (milliseconds + callback →
    /// cancellation handle) instead of a real <see cref="Task.Delay(int, CancellationToken)"/>.
    /// Production hosts leave it null.
    /// </summary>
    internal static Func<int, Action, IDisposable>? Scheduler;

    /// <summary>Schedules <paramref name="callback"/> to run once after <paramref name="milliseconds"/>.</summary>
    /// <param name="milliseconds">The delay before the callback runs.</param>
    /// <param name="callback">The work to run when the delay elapses.</param>
    /// <returns>A handle; dispose it to cancel the timer before it fires.</returns>
    internal static IDisposable Schedule(int milliseconds, Action callback)
    {
        var scheduler = Scheduler;
        if (scheduler is not null)
        {
            return scheduler(milliseconds, callback);
        }
        var timer = new RealTimer();
        timer.Start(milliseconds, callback);
        return timer;
    }

    private sealed class RealTimer : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        internal void Start(int milliseconds, Action callback)
            => _ = RunAsync(milliseconds, callback, _cancellation.Token);

        private static async Task RunAsync(int milliseconds, Action callback, CancellationToken token)
        {
            try
            {
                // Context-capturing await (no ConfigureAwait(false)): the continuation resumes on the
                // single-threaded WASM synchronization context, matching the async-component contract.
                await Task.Delay(milliseconds, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (!token.IsCancellationRequested)
            {
                callback();
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
