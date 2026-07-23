using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// Schedules the loading delay and timeout used by asynchronous components.
/// </summary>
/// <remarks>Ambient and single-threaded; tests replace the scheduler with virtual time.</remarks>
internal static class AsynchronousComponentDelay
{
    internal static Func<int, Action, IDisposable>? Scheduler;

    internal static IDisposable Schedule(int milliseconds, Action callback)
    {
        Func<int, Action, IDisposable>? scheduler = Scheduler;
        if (scheduler is not null)
        {
            return scheduler(milliseconds, callback);
        }

        DelayTimer timer = new();
        timer.Start(milliseconds, callback);
        return timer;
    }

    private sealed class DelayTimer : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        internal void Start(int milliseconds, Action callback)
        {
            _ = RunAsync(milliseconds, callback, _cancellation.Token);
        }

        private static async Task RunAsync(
            int milliseconds,
            Action callback,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(milliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!cancellationToken.IsCancellationRequested)
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
